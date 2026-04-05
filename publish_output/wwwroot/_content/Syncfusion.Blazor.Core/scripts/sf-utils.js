(function () {
    // Prevent multiple initializations if the script is accidentally loaded more than once
    if (window.SfStyleWatcher) return;

    const DATA_STYLE_ATTR = 'data-sf-style';
    const LAYOUT_CRITICAL_PROPS = {
        width: true,
        height: true,
        'min-width': true,
        'max-width': true,
        'min-height': true,
        'max-height': true,
        top: true,
        left: true,
        right: true,
        bottom: true,
        display: true,
        position: true,
        transform: true
    };

    /**
     * Minimal microtask scheduler to batch multiple style writes into a single batch.
     * queueMicrotask finishes before the browser does things like rendering the page Ensures no flicker.
     */
    const queue = [];
    let queued = false; // flag
    /**
     * Schedule a function to run in the next microtask batch.
     * Multiple calls before the batch executes will queue multiple functions.
     */
    function schedule(fn) {
        queue.push(fn);
        if (!queued) {
            queued = true;
            queueMicrotask(() => {
                try {
                    // Execute all queued style updates in order
                    for (let i = 0; i < queue.length; i++) {
                        queue[i]()
                    }
                } finally {
                    // Always reset the queue and flag
                    queue.length = 0;
                    queued = false;
                }
            });
        }
    }

    /**
     * Flush queued style operations immediately.
     * Useful when deterministic style reads are required.
     */
    function flushScheduled() {
        if (!queue.length) {
            queued = false;
            return;
        }

        const pending = queue.splice(0, queue.length);
        queued = false;
        for (let i = 0; i < pending.length; i++) {
            pending[i]();
        }
    }

    /**
     * Parse a CSS style text into a property map.
     * Handles !important priority and trims whitespace.
     *
     * @param {string} styleText - Raw style text
     * @returns {Object<string, { value: string, priority: string }>} - Parsed styles
     */
    function parseStyleText(styleText) {
        const map = {};
        if (!styleText) return map;
        const parts = styleText.split(';');
        for (let i = 0; i < parts.length; i++) {
            const part = parts[i].trim();
            if (!part) continue;
            const colonIndex = part.indexOf(':');
            if (colonIndex === -1) continue;
            const prop = part.slice(0, colonIndex).trim();
            let value = part.slice(colonIndex + 1).trim();
            let priority = '';
            if (/!important$/i.test(value)) {
                value = value.replace(/!important$/i, '').trim();
                priority = 'important';
            }
            if (prop) {
                map[prop] = { value, priority };
            }
        }
        return map;
    }

    /**
     * Returns true when style map includes layout-critical properties.
     * @param {Object<string, { value: string, priority: string }>} map - Parsed style map.
     * @returns {boolean} - Whether immediate application is required.
     */
    function hasLayoutCriticalProps(map) {
        const keys = Object.keys(map);
        for (let i = 0; i < keys.length; i++) {
            if (LAYOUT_CRITICAL_PROPS[keys[i].toLowerCase()]) {
                return true;
            }
        }
        return false;
    }

    /**
     * Apply inline styles from the data-sf-style attribute without overriding
     * styles set by component scripts. Only properties present in data-sf-style
     * are updated, and previously applied properties are removed when absent.
     *
     * Uses cached values (sfAppliedStyleText/sfAppliedStyleMap) to skip redundant writes.
     * All actual DOM writes are deferred to the microtask scheduler to avoid flicker.
     *
     * JS-override detection: if component JS has changed a property's value since
     * sf-utils last applied it (i.e. current inline value differs from cached value),
     * that property is considered "JS-owned" and will not be overwritten or removed.
     * This prevents sf-utils from fighting against component scripts (e.g. sf-dialog.js
     * setting display:block on the overlay after sf-utils set display:none from
     * data-sf-style).
     *
     * @param {Element} element - The DOM element to style
     * @param {string|null} styleValue - The raw value of the data-sf-style attribute
     */
    function applySfStyle(element, styleValue, forceSync) {
        const newCss = styleValue || '';
        const prevCss = element.sfAppliedStyleText || '';

        // Skip if the style string hasn't changed
        if (newCss === prevCss) return;

        const newMap = parseStyleText(newCss);
        const prevMap = element.sfAppliedStyleMap || {};
        const shouldApplySync = !!forceSync || hasLayoutCriticalProps(newMap) || hasLayoutCriticalProps(prevMap);

        // Snapshot the current inline style map at the moment data-sf-style is processed
        // (synchronous, before the microtask runs). This is the baseline for first-time
        // properties (prevEntry === undefined): if a property's value changes between
        // this snapshot and when the microtask executes, component JS set it in the
        // interim and must not be overwritten.
        const scheduleTimeMap = parseStyleText(element.style.cssText);

        const applyStyles = () => {
            // Remove properties that were previously applied but no longer present,
            // but only if component JS has not overridden the value since we last set it.
            Object.keys(prevMap).forEach(prop => {
                if (!newMap.hasOwnProperty(prop)) {
                    const currentValue = element.style.getPropertyValue(prop);
                    // Safe to remove only when the value still matches what sf-utils last wrote.
                    // If it differs, JS has changed it — leave it alone.
                    if (currentValue === prevMap[prop].value) {
                        element.style.removeProperty(prop);
                    }
                }
            });

            // Set/update current properties from data-sf-style,
            // but only if component JS has not overridden the value since data-sf-style was stamped.
            Object.keys(newMap).forEach(prop => {
                const entry = newMap[prop];
                const currentValue = element.style.getPropertyValue(prop);
                const prevEntry = prevMap[prop];

                let jsOverrode;
                if (prevEntry !== undefined) {
                    // sf-utils previously managed this property.
                    // JS-overridden if the current value is non-empty and differs from what sf-utils last wrote.
                    jsOverrode = currentValue !== '' && currentValue !== prevEntry.value;
                } else if (element.sfAppliedStyleMap !== undefined) {
                    // sf-utils manages this element but has never seen this specific property before.
                    // Use the schedule-time snapshot as the baseline: if the value changed between
                    // snapshot time and microtask execution, component JS set it — respect it.
                    const scheduleTimeValue = scheduleTimeMap[prop] ? scheduleTimeMap[prop].value : '';
                    jsOverrode = currentValue !== '' && currentValue !== scheduleTimeValue;
                } else {
                    // sf-utils has NEVER managed this element at all (sfAppliedStyleMap is absent).
                    // Any pre-existing inline value must be treated as JS-owned unconditionally.
                    // The schedule-time snapshot cannot be used here: if JS set the value before
                    // applySfStyle was ever called (e.g. Gantt JS sets splitter height before
                    // Blazor re-stamps data-sf-style after a cache-clearing re-mount in WASM),
                    // scheduleTimeValue === currentValue, so the snapshot check would incorrectly
                    // report jsOverrode=false and overwrite the JS value with the Blazor default.
                    jsOverrode = currentValue !== '';
                }

                if (!jsOverrode) {
                    element.style.setProperty(prop, entry.value, entry.priority);
                }
            });

            // Update the cache to reflect what is actually in the element now.
            // For JS-overridden properties, record the live JS value so that future
            // data-sf-style re-stamps can correctly detect further JS changes.
            const updatedMap = {};
            Object.keys(newMap).forEach(prop => {
                const currentValue = element.style.getPropertyValue(prop);
                updatedMap[prop] = {
                    value: currentValue,
                    priority: element.style.getPropertyPriority(prop)
                };
            });
            element.sfAppliedStyleText = newCss;
            element.sfAppliedStyleMap = updatedMap;

            if (element.hasAttribute('data-sf-style')) {
                // Mark intentional removal so the observer does not call clearSfStyle
                element.isDataAttrRemoved = true;
                element.removeAttribute(DATA_STYLE_ATTR);
                queueMicrotask(() => { delete element.isDataAttrRemoved; });
            }
        };

        // Apply immediately for layout-critical updates to avoid stale reads (WASM/server parity).
        // Keep batching for non-critical properties to reduce style churn and flicker.
        if (shouldApplySync) {
            applyStyles();
        } else {
            schedule(applyStyles);
        }
    }

    /**
       * Clear inline styles that were previously applied by this script.
       * Important: Only clears if we previously set something (checks cache).
       * This prevents accidentally wiping user- or framework-defined inline styles.
       * 
       * @param {Element} element - The DOM element to potentially clear
       */
    function clearSfStyle(element, forceSync) {
        // Only act if this script previously applied a style to this element
        if (element.sfAppliedStyleMap !== undefined || element.sfAppliedStyleText !== undefined) {
            const clearStyles = () => {
                const prevMap = element.sfAppliedStyleMap || {};
                Object.keys(prevMap).forEach(prop => {
                    const currentValue = element.style.getPropertyValue(prop);
                    // Only remove if the value still matches what sf-utils last wrote.
                    // If JS changed it, leave it alone.
                    if (currentValue === prevMap[prop].value) {
                        element.style.removeProperty(prop);
                    }
                });
                // Remove cache entries to indicate we no longer manage this element's styles
                delete element.sfAppliedStyleText;
                delete element.sfAppliedStyleMap;
            };

            if (forceSync) {
                clearStyles();
            } else {
                schedule(clearStyles);
            }
        }
    }

    /**
     * Patch native attribute APIs so data-sf-style writes become immediately observable.
     * This removes timing differences between render modes when components read dimensions
     * right after render.
     */
    const nativeSetAttribute = Element.prototype.setAttribute;
    const nativeRemoveAttribute = Element.prototype.removeAttribute;
    Element.prototype.setAttribute = function (name, value) {
        nativeSetAttribute.call(this, name, value);
        if (name === DATA_STYLE_ATTR && this.nodeType === 1) {
            applySfStyle(this, value, true);
        }
    };

    Element.prototype.removeAttribute = function (name) {
        const hadDataStyle = name === DATA_STYLE_ATTR && this.hasAttribute(DATA_STYLE_ATTR);
        nativeRemoveAttribute.call(this, name);
        if (hadDataStyle && this.nodeType === 1 && !this.isDataAttrRemoved) {
            clearSfStyle(this, true);
        }
    };

    /**
     * Process a single element if it has data-sf-style.
     */
    function processElement(element) {
        if (!element || element.nodeType !== 1) return;
        if (element.hasAttribute(DATA_STYLE_ATTR)) {
            applySfStyle(element, element.getAttribute(DATA_STYLE_ATTR), true);
        }
    }

    /**
     * Recursively process a subtree for elements with data-sf-style attributes.
     * 
     * Used both on initial load and when new nodes are added to the DOM.
     * 
     * @param {Node} root - The root node of the subtree (usually an Element)
     */
    function processTree(root) {
        if (!root || root.nodeType !== 1) return;
        // Process root itself
        processElement(root);
        // Process descendants
        if (root.querySelectorAll) {
            root.querySelectorAll('[data-sf-style]').forEach(processElement);
        }
    }

    /**
       * MutationObserver callback - reacts to DOM changes.
       * 
       * Handles:
       * New nodes being added (childList mutations)
       * Changes to data-sf-style attribute
       */
    const observer = new MutationObserver(mutationsList => {
        for (const mutation of mutationsList) {
            if (mutation.type === 'childList') {
                // New nodes added
                mutation.addedNodes.forEach(node => processTree(node));
            } else if (mutation.type === 'attributes') {
                const element = mutation.target;
                if (mutation.attributeName === DATA_STYLE_ATTR) {
                    const styleValue = element.getAttribute(DATA_STYLE_ATTR);
                    // Attribute removed
                    if (styleValue === null) {
                        // To ensure if its not called from removal of data attr we intentionally did
                        if (!element.isDataAttrRemoved)
                            clearSfStyle(element, true);
                    } else {
                        // Attribute changed/added
                        applySfStyle(element, styleValue, true);
                    }
                } else if (mutation.attributeName === 'style') {
                    if (element.hasAttribute(DATA_STYLE_ATTR)) {
                        // data-sf-style still present: re-apply our styles to prevent visible flicker.
                        // JS-override detection inside applySfStyle guards individual properties.
                        applySfStyle(element, element.getAttribute(DATA_STYLE_ATTR), true);
                    } else if (element.sfAppliedStyleMap !== undefined) {
                        // data-sf-style already removed by sf-utils, but component JS just changed
                        // the inline style. Sync the cache to the current live values so that
                        // when Blazor re-renders and re-adds data-sf-style, applySfStyle correctly
                        // identifies JS-overridden properties and leaves them untouched.
                        const liveMap = {};
                        Object.keys(element.sfAppliedStyleMap).forEach(prop => {
                            liveMap[prop] = {
                                value: element.style.getPropertyValue(prop),
                                priority: element.style.getPropertyPriority(prop)
                            };
                        });
                        element.sfAppliedStyleMap = liveMap;
                        // Keep sfAppliedStyleText unchanged — it still reflects the last
                        // data-sf-style string so applySfStyle can detect a re-stamp.
                    }
                }
            }
        }
    });

    // Observe the entire document for subtree changes, new nodes, and specific attributes
    observer.observe(document.documentElement, {
        subtree: true,
        childList: true,
        attributes: true,
        // Limited attribute monitoring to only what we need
        attributeFilter: [DATA_STYLE_ATTR, 'style'] // narrow filter for performance
    });

    // Process the existing DOM once at startup
    processTree(document.documentElement);

    /**
       * Public API exposed on window.SfStyleWatcher
       * 
       * Useful for manual refresh
       */
    window.SfStyleWatcher = {
        refresh(root = document.documentElement) {
            processTree(root);
        },
        flush() {
            flushScheduled();
        },
        // Stop observing mutations
        disconnect() {
            observer.disconnect();
        }
    };
})();