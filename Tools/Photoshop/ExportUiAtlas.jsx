#target photoshop

(function () {
    try {
        exportAtlas();
    } catch (error) {
        alert("UI atlas export failed.\n\n" + error.message);
    }

    function exportAtlas() {
        if (app.documents.length === 0) {
            throw new Error("Open a Photoshop document first.");
        }

        var source = app.activeDocument;
        var regions = collectRegions(source);
        var destination = Folder.selectDialog("Choose the atlas output folder");
        if (!destination) {
            return;
        }

        var stem = source.name.replace(/\.[^.]+$/, "");
        var png = new File(destination.fsName + "/" + stem + ".png");
        var json = new File(destination.fsName + "/" + stem + ".json");

        if ((png.exists || json.exists) && !confirm("Replace the existing atlas files for " + stem + "?")) {
            return;
        }

        var staging = new Folder(Folder.temp.fsName + "/ShockGL-Atlas-" + new Date().getTime());
        if (staging.exists || !staging.create()) {
            throw new Error("Could not create a temporary export folder.");
        }

        var stagedPng = new File(staging.fsName + "/atlas.png");
        var stagedJson = new File(staging.fsName + "/atlas.json");
        var pngBackup = new File(staging.fsName + "/previous.png");
        var jsonBackup = new File(staging.fsName + "/previous.json");
        var keepBackups = false;

        try {
            var copy = source.duplicate("UI atlas export", true);
            try {
                copy.saveAs(stagedPng, new PNGSaveOptions(), false, Extension.LOWERCASE);
            } finally {
                copy.close(SaveOptions.DONOTSAVECHANGES);
                app.activeDocument = source;
            }
            writeJson(stagedJson, regions);

            var hadPng = png.exists;
            var hadJson = json.exists;
            if ((hadPng && !png.copy(pngBackup.fsName)) || (hadJson && !json.copy(jsonBackup.fsName))) {
                throw new Error("Could not back up the existing atlas files.");
            }

            if (!stagedPng.copy(png.fsName) || !stagedJson.copy(json.fsName)) {
                var restoredPng = restoreFile(png, pngBackup, hadPng);
                var restoredJson = restoreFile(json, jsonBackup, hadJson);
                keepBackups = !restoredPng || !restoredJson;
                throw new Error(keepBackups
                    ? "Could not replace the atlas. Backups are in " + staging.fsName
                    : "Could not write the atlas files.");
            }
        } finally {
            if (!keepBackups) {
                stagedPng.remove();
                stagedJson.remove();
                pngBackup.remove();
                jsonBackup.remove();
                staging.remove();
            }
        }

        alert("Exported " + regions.length + " UI atlas regions.\n" + png.fsName + "\n" + json.fsName);
    }

    function collectRegions(document) {
        var width = document.width.as("px");
        var height = document.height.as("px");
        var regions = [];

        for (var i = 0; i < document.layers.length; i++) {
            var layer = document.layers[i];
            if (!layer.visible) {
                continue;
            }

            var bounds = layer.bounds;
            var left = Math.floor(bounds[0].as("px"));
            var top = Math.floor(bounds[1].as("px"));
            var right = Math.ceil(bounds[2].as("px"));
            var bottom = Math.ceil(bounds[3].as("px"));
            if (right <= left || bottom <= top) {
                continue;
            }

            var name = layer.name.replace(/^\s+|\s+$/g, "");
            if (!name) {
                throw new Error("Every visible, nonempty layer or group needs a name.");
            }
            if (left < 0 || top < 0 || right > width || bottom > height) {
                throw new Error("Region '" + name + "' extends outside the canvas.");
            }

            for (var j = 0; j < regions.length; j++) {
                var other = regions[j];
                if (name === other.name) {
                    throw new Error("Duplicate region name '" + name + "'.");
                }
                if (left < other.right && right > other.left && top < other.bottom && bottom > other.top) {
                    throw new Error("Regions '" + name + "' and '" + other.name + "' overlap.");
                }
            }

            regions.push({ name: name, left: left, top: top, right: right, bottom: bottom });
        }

        if (regions.length === 0) {
            throw new Error("The document has no visible, nonempty layers or groups.");
        }
        return regions;
    }

    function writeJson(file, regions) {
        // ExtendScript does not always provide JSON.stringify.
        var entries = [];
        for (var i = 0; i < regions.length; i++) {
            var region = regions[i];
            var name = region.name.replace(/["\\\x00-\x1f]/g, function (character) {
                return "\\u" + ("0000" + character.charCodeAt(0).toString(16)).slice(-4);
            });
            entries.push('    "' + name + '": { "X": ' + region.left + ', "Y": ' + region.top +
                ', "Width": ' + (region.right - region.left) + ', "Height": ' + (region.bottom - region.top) + ' }');
        }

        file.encoding = "UTF-8";
        if (!file.open("w")) {
            throw new Error("Could not open the temporary JSON file.");
        }
        try {
            if (!file.write('{\n  "Regions": {\n' + entries.join(",\n") + '\n  }\n}\n')) {
                throw new Error("Could not write the atlas JSON.");
            }
        } finally {
            file.close();
        }
    }

    function restoreFile(file, backup, existed) {
        return existed ? backup.copy(file.fsName) : (!file.exists || file.remove());
    }
}());
