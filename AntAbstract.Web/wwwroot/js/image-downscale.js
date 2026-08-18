// Profil fotoğrafını gönderilmeden önce tarayıcıda küçültür.
//
// Telefon kameraları 15-20 MB'lık JPEG üretiyor ve kullanıcılar sunucudaki
// boyut sınırına takılıp kayıt olamıyordu. Fotoğrafı burada ölçekleyerek
// hem sınır sorunu ortadan kalkıyor hem de mobil bağlantıda yükleme hızlanıyor.
//
// Sunucudaki doğrulama yerinde duruyor; bu yalnızca kullanıcıyı rahatlatan
// bir ön adım, güvenlik kontrolü değil.
(function () {
    "use strict";

    var MAX_EDGE = 1024;      // uzun kenar (piksel)
    var QUALITY = 0.85;       // JPEG kalitesi
    var SKIP_BELOW = 1048576; // 1 MB altındakilere dokunma

    function canDownscale() {
        return typeof HTMLCanvasElement !== "undefined" &&
               typeof DataTransfer !== "undefined" &&
               typeof FileReader !== "undefined";
    }

    function formatSize(bytes) {
        return (bytes / 1048576).toFixed(1) + " MB";
    }

    function showNote(input, text) {
        var id = "downscale-note-" + (input.id || "profile");
        var note = document.getElementById(id);

        if (!note) {
            note = document.createElement("div");
            note.id = id;
            note.className = "text-success small mt-1";
            input.insertAdjacentElement("afterend", note);
        }

        note.textContent = text;
    }

    function replaceFile(input, blob, originalName, originalSize) {
        var name = originalName.replace(/\.[^.]+$/, "") + ".jpg";
        var file = new File([blob], name, {
            type: "image/jpeg",
            lastModified: Date.now()
        });

        var transfer = new DataTransfer();
        transfer.items.add(file);
        input.files = transfer.files;

        showNote(
            input,
            "Fotoğraf otomatik küçültüldü: " +
            formatSize(originalSize) + " → " + formatSize(file.size));
    }

    function handle(input) {
        var file = input.files && input.files[0];

        if (!file || file.type.indexOf("image/") !== 0 || file.size < SKIP_BELOW) {
            return;
        }

        var originalName = file.name;
        var originalSize = file.size;
        var reader = new FileReader();

        reader.onload = function (e) {
            var img = new Image();

            img.onload = function () {
                var scale = Math.min(1, MAX_EDGE / Math.max(img.width, img.height));

                // Zaten küçükse yeniden kodlamak yine de boyutu düşürür,
                // bu yüzden ölçek 1 olsa da devam ediyoruz.
                var canvas = document.createElement("canvas");
                canvas.width = Math.round(img.width * scale);
                canvas.height = Math.round(img.height * scale);

                var ctx = canvas.getContext("2d");
                ctx.drawImage(img, 0, 0, canvas.width, canvas.height);

                canvas.toBlob(function (blob) {
                    // Küçültme işe yaramadıysa orijinali bırak.
                    if (blob && blob.size < originalSize) {
                        replaceFile(input, blob, originalName, originalSize);
                    }
                }, "image/jpeg", QUALITY);
            };

            img.onerror = function () { /* bozuk görsel: sunucu yakalar */ };
            img.src = e.target.result;
        };

        reader.onerror = function () { /* okunamadı: sunucu yakalar */ };
        reader.readAsDataURL(file);
    }

    function attach() {
        if (!canDownscale()) {
            return;
        }

        var inputs = document.querySelectorAll('input[type="file"][data-downscale]');

        Array.prototype.forEach.call(inputs, function (input) {
            input.addEventListener("change", function () { handle(input); });
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", attach);
    } else {
        attach();
    }
})();
