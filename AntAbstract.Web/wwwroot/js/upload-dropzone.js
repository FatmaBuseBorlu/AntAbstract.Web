(() => {
    function initUploadDropzones() {
        document.querySelectorAll(".upload-zone").forEach((zone) => {
            const input = zone.querySelector('input[type="file"]');

            if (!input || zone.dataset.dropzoneReady === "true") {
                return;
            }

            zone.dataset.dropzoneReady = "true";

            ["dragenter", "dragover"].forEach((eventName) => {
                zone.addEventListener(eventName, (event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    zone.classList.add("drag-over");
                });
            });

            ["dragleave", "dragend"].forEach((eventName) => {
                zone.addEventListener(eventName, (event) => {
                    if (event.type === "dragleave" && event.relatedTarget && zone.contains(event.relatedTarget)) {
                        return;
                    }

                    zone.classList.remove("drag-over");
                });
            });

            zone.addEventListener("drop", (event) => {
                event.preventDefault();
                event.stopPropagation();
                zone.classList.remove("drag-over");

                const files = event.dataTransfer?.files;

                if (!files || files.length === 0) {
                    return;
                }

                const transfer = new DataTransfer();
                const selectedFiles = input.multiple ? Array.from(files) : [files[0]];

                selectedFiles.forEach((file) => transfer.items.add(file));
                input.files = transfer.files;
                input.dispatchEvent(new Event("change", { bubbles: true }));
            });
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initUploadDropzones);
    } else {
        initUploadDropzones();
    }
})();
