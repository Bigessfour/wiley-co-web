/**
 * Blazor WebAssembly boot loader for Wiley Widget.
 * Follows ASP.NET Core guidance: autostart=false, Blazor.start({ loadBootResource }).
 * @see https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/webassembly#control-the-load-behavior
 */
(function () {
  "use strict";

  var headline = document.getElementById("wiley-static-boot-headline");
  var detail = document.getElementById("wiley-static-boot-detail");
  var progressBar = document.getElementById("wiley-static-boot-progress");
  var progressLabel = document.getElementById(
    "wiley-static-boot-progress-label",
  );
  var errorUi = document.getElementById("blazor-error-ui");

  var resourcesStarted = 0;
  var totalResources = 0;
  var lastProgressAt = Date.now();
  var stallTimer = null;
  var FETCH_TIMEOUT_MS = 60000;

  window.__wileyBootLogs = window.__wileyBootLogs || [];

  function logBootFailure(message) {
    window.__wileyBootLogs.push({
      at: new Date().toISOString(),
      message: message,
    });
  }

  function fetchWithTimeout(url, options, timeoutMs) {
    var controller = new AbortController();
    var timer = setTimeout(function () {
      controller.abort();
    }, timeoutMs);
    var merged = Object.assign({}, options || {}, {
      signal: controller.signal,
    });
    return fetch(url, merged).finally(function () {
      clearTimeout(timer);
    });
  }

  if (errorUi) {
    var dismiss = errorUi.querySelector(".dismiss");
    if (dismiss) {
      dismiss.addEventListener("click", function () {
        errorUi.style.display = "none";
      });
    }
  }

  function showErrorUi(message) {
    if (errorUi) {
      errorUi.style.display = "block";
      var messageNode = errorUi.querySelector(".blazor-error-message");
      if (messageNode) {
        messageNode.textContent = message;
      }
    }
  }

  function setProgress(started, total, label) {
    lastProgressAt = Date.now();
    var pct =
      total > 0 ? Math.min(100, Math.round((started / total) * 100)) : 0;
    if (progressBar) {
      progressBar.style.width = pct + "%";
      progressBar.setAttribute("aria-valuenow", String(pct));
    }
    if (progressLabel && label) {
      progressLabel.textContent = label;
    }
    if (headline && total > 0) {
      headline.textContent = "Loading Wiley Widget (" + pct + "%)";
    }
  }

  function showBootError(message) {
    if (headline) headline.textContent = "Wiley Widget failed to start";
    if (detail) detail.textContent = message;
    if (progressLabel) {
      progressLabel.textContent =
        "Close this tab (Cmd/Ctrl+W), then re-launch from terminal: ./Scripts/start-chrome-debug.sh (clean profile) or hard-refresh a *new* tab. Open DevTools → Console + Network (filter _framework) for the exact failing file.";
    }
    showErrorUi(message);
    logBootFailure(message);
    console.error("[WileyCoWeb boot]", message);
  }

  function countBootResources(boot) {
    var resources = boot && boot.resources ? boot.resources : {};
    if (resources.fingerprinting) {
      return Object.keys(resources.fingerprinting).length;
    }
    if (resources.wasm) {
      return Object.keys(resources.wasm).length;
    }
    return 0;
  }

  function findMainAssemblyWasmKey(boot) {
    var main = boot && boot.mainAssemblyName;
    var fp = boot && boot.resources && boot.resources.fingerprinting;
    if (!main || !fp) return null;
    var keys = Object.keys(fp);
    for (var i = 0; i < keys.length; i++) {
      var key = keys[i];
      if (key.indexOf(main + ".") === 0 && key.endsWith(".wasm")) {
        return key;
      }
    }
    return null;
  }

  function verifyMainAssemblyReachable(boot) {
    var wasmKey = findMainAssemblyWasmKey(boot);
    if (!wasmKey) {
      return Promise.resolve();
    }
    var url = "_framework/" + wasmKey;
    return fetchWithTimeout(
      url,
      { method: "HEAD", cache: "no-store" },
      FETCH_TIMEOUT_MS,
    ).then(function (response) {
      if (!response.ok) {
        throw new Error(
          "Main assembly " +
            wasmKey +
            " returned HTTP " +
            response.status +
            ". " +
            "The Blazor dev server is serving a stale manifest — CLOSE this tab completely, then run ./stop-local.sh && ./start-local.sh (or ./Scripts/start-chrome-debug.sh). Do not run dotnet build while dotnet run is active without dotnet watch.",
        );
      }
    });
  }

  function startStallWatch() {
    if (stallTimer) {
      clearInterval(stallTimer);
    }
    stallTimer = setInterval(function () {
      if (Date.now() - lastProgressAt < 90000) {
        return;
      }
      if (progressLabel) {
        progressLabel.textContent =
          "Still loading (or stuck on early runtime like dotnet.js). CLOSE THIS TAB completely, then: ./stop-local.sh && ./start-local.sh (or ./Scripts/start-chrome-debug.sh for a fresh Chrome with no cache). Hard refresh is often not enough for a stale manifest.";
      }
    }, 15000);
  }

  // Blazor requires a URI string (or null) for these types — returning fetch() breaks boot with:
  // "For a dotnetjs resource, custom loaders must supply a URI string."
  var uriOnlyBootResourceTypes = {
    dotnetjs: true,
    "js-module-runtime": true,
    "js-module-dotnet": true,
  };

  function loadBootResource(type, name, defaultUri, integrity) {
    resourcesStarted += 1;
    var label = name || type || defaultUri;
    setProgress(
      resourcesStarted,
      totalResources || resourcesStarted,
      "Loading " + label + "…",
    );

    if (uriOnlyBootResourceTypes[type]) {
      return defaultUri;
    }

    var fetchOptions = { cache: "no-store" };
    if (integrity) {
      fetchOptions.integrity = integrity;
    }

    return fetchWithTimeout(defaultUri, fetchOptions, FETCH_TIMEOUT_MS)
      .then(function (response) {
        if (!response.ok) {
          throw new Error(
            "Failed to load " +
              label +
              " (HTTP " +
              response.status +
              "). " +
              "CLOSE this tab completely and restart the stack (./stop-local.sh && ./start-local.sh) or use ./Scripts/start-chrome-debug.sh for a clean Chrome profile. Early runtime files (dotnet.js etc.) are the most common cause of 0-2% hangs.",
          );
        }
        return response;
      })
      .catch(function (err) {
        if (err && err.name === "AbortError") {
          throw new Error(
            "Timed out loading " +
              label +
              " after " +
              FETCH_TIMEOUT_MS / 1000 +
              "s. " +
              "The dev server on port 5230 may be a zombie listener — run ./stop-local.sh && ./start-local.sh, then open a fresh tab via ./Scripts/start-chrome-debug.sh.",
          );
        }
        throw err;
      });
  }

  function startBlazor(boot) {
    totalResources = countBootResources(boot);
    setProgress(
      0,
      totalResources,
      "Downloading " + totalResources + " runtime modules…",
    );
    startStallWatch();

    return Blazor.start({
      loadBootResource: loadBootResource,
    }).then(function () {
      if (stallTimer) {
        clearInterval(stallTimer);
      }
      setProgress(totalResources, totalResources, "Starting workspace shell…");
    });
  }

  fetchWithTimeout(
    "_framework/blazor.boot.json",
    { cache: "no-store" },
    FETCH_TIMEOUT_MS,
  )
    .then(function (response) {
      if (!response.ok) {
        throw new Error("blazor.boot.json HTTP " + response.status);
      }
      return response.json();
    })
    .then(function (boot) {
      return verifyMainAssemblyReachable(boot).then(function () {
        return boot;
      });
    })
    .then(startBlazor)
    .catch(function (err) {
      if (stallTimer) {
        clearInterval(stallTimer);
      }
      showBootError(
        (err && err.message) ||
          "Blazor WebAssembly did not start. CLOSE this tab and restart the full stack (./stop-local.sh && ./start-local.sh) or launch a clean browser with ./Scripts/start-chrome-debug.sh.",
      );
    });
})();
