// Helpers de JS interop chamados pelo Blazor via IJSRuntime.InvokeVoidAsync.
window.clickElementById = (id) => {
    const el = document.getElementById(id);
    if (el) el.click();
};

// Baixa um arquivo binário (base64) sem round-trip ao servidor.
window.downloadFile = (fileName, base64, contentType) => {
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const blob = new Blob([bytes], { type: contentType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
};
