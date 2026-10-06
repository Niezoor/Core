mergeInto(LibraryManager.library, {
    // Width of the game canvas in CSS pixels; 0 when unknown.
    CoreDisplay_GetCanvasCssWidth: function () {
        var canvas = Module.canvas;
        return canvas && canvas.clientWidth ? canvas.clientWidth : 0;
    }
});
