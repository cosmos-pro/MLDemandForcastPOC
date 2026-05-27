// Helpers de JS interop chamados pelo Blazor via IJSRuntime.InvokeVoidAsync.
window.clickElementById = (id) => {
    const el = document.getElementById(id);
    if (el) el.click();
};
