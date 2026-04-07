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
