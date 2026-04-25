globalThis.wileyWorkspaceStorage = {
  getItem: function (key) {
    return globalThis.localStorage.getItem(key);
  },
  setItem: function (key, value) {
    globalThis.localStorage.setItem(key, value);
  },
  removeItem: function (key) {
    globalThis.localStorage.removeItem(key);
  },
};

// ─────────────────────────────────────────────────────────────────────────────
// wileyLayout — viewport observer for WorkspaceLayoutContext
//
// MainLayout.razor calls:
//   wileyLayout.getWindowWidth()          → returns current innerWidth (int)
//   wileyLayout.subscribeResize(dotNetRef) → registers a debounced listener
//   wileyLayout.unsubscribeResize()        → removes the listener on disposal
//
// The debounce (150 ms) prevents excessive .NET invocations during a live
// resize drag.  The DotNetObjectReference passed to subscribeResize is the
// LayoutJsBridge wrapper, which has a [JSInvokable] OnWindowResized(int).
// ─────────────────────────────────────────────────────────────────────────────
globalThis.wileyLayout = (function () {
  let _dotNetRef = null;
  let _debounceHandle = null;
  const DEBOUNCE_MS = 150;

  function handleResize() {
    clearTimeout(_debounceHandle);
    _debounceHandle = setTimeout(function () {
      if (_dotNetRef) {
        _dotNetRef.invokeMethodAsync("OnWindowResized", globalThis.innerWidth);
      }
    }, DEBOUNCE_MS);
  }

  return {
    /** Returns the current viewport width so MainLayout can set the initial LayoutMode. */
    getWindowWidth: function () {
      return globalThis.innerWidth;
    },

    /**
     * Registers a debounced resize listener that calls back into .NET.
     * @param {DotNetObjectReference} dotNetRef – reference to LayoutJsBridge.
     */
    subscribeResize: function (dotNetRef) {
      _dotNetRef = dotNetRef;
      globalThis.addEventListener("resize", handleResize);
    },

    /**
     * Removes the resize listener.  Called from MainLayout.DisposeAsync so
     * the JS handler is cleaned up when the Blazor component tears down.
     */
    unsubscribeResize: function () {
      globalThis.removeEventListener("resize", handleResize);
      clearTimeout(_debounceHandle);
      _dotNetRef = null;
      _debounceHandle = null;
    },
  };
})();

globalThis.wileyDownloads = {
  saveFileFromBase64: function (fileName, contentType, base64) {
    const binary = globalThis.atob(base64);
    const bytes = new Uint8Array(binary.length);

    for (let index = 0; index < binary.length; index++) {
      bytes[index] = binary.codePointAt(index);
    }

    const blob = new Blob([bytes], { type: contentType });
    const url = globalThis.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    globalThis.URL.revokeObjectURL(url);
  },
};

// ─────────────────────────────────────────────────────────────────────────────
// wileyNetworkStatus — navigator.onLine bridge for WileyWorkspaceBase
//
// WileyWorkspaceBase calls:
//   wileyNetworkStatus.isOnline()               → returns navigator.onLine (bool)
//   wileyNetworkStatus.subscribe(dotNetRef)     → registers online/offline listeners
//   wileyNetworkStatus.unsubscribe()            → removes listeners on disposal
//
// The DotNetObjectReference must expose a [JSInvokable] method named
// "HandleOnlineStatusChanged" that receives a single boolean argument.
// ─────────────────────────────────────────────────────────────────────────────
globalThis.wileyNetworkStatus = (function () {
  let _dotNetRef = null;

  function onOnline() {
    if (_dotNetRef)
      _dotNetRef.invokeMethodAsync("HandleOnlineStatusChanged", true);
  }
  function onOffline() {
    if (_dotNetRef)
      _dotNetRef.invokeMethodAsync("HandleOnlineStatusChanged", false);
  }

  return {
    /** Returns the current navigator.onLine value. */
    isOnline: function () {
      return navigator.onLine;
    },

    /**
     * Subscribes to browser online/offline events and forwards them to .NET.
     * @param {DotNetObjectReference} dotNetRef – reference to WileyWorkspaceBase.
     */
    subscribe: function (dotNetRef) {
      _dotNetRef = dotNetRef;
      globalThis.addEventListener("online", onOnline);
      globalThis.addEventListener("offline", onOffline);
    },

    /** Removes the online/offline listeners.  Call from Dispose. */
    unsubscribe: function () {
      globalThis.removeEventListener("online", onOnline);
      globalThis.removeEventListener("offline", onOffline);
      _dotNetRef = null;
    },
  };
})();
