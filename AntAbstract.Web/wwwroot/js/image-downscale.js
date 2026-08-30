// Profil fotoğrafını gönderilmeden önce tarayıcıda küçültür.
//
// Telefon kameraları 20 MB'ı aşan JPEG üretiyor; küçültme olmadan kullanıcılar
// sunucudaki boyut sınırına takılıp kayıt olamıyordu. Fotoğrafı burada
// ölçekleyince sınır pratikte hiç devreye girmiyor, mobil yükleme de hızlanıyor.
//
// Sunucudaki doğrulama yerinde duruyor; bu yalnızca kullanıcıyı rahatlatan bir
// ön adım, güvenlik kontrolü değil. Bu yüzden her hata durumunda sessizce
// orijinal dosyaya dönülür — kullanıcı asla burada takılmamalı.
(function () {
    "use strict";

    var MAX_EDGE = 1024;      // uzun kenar (piksel)
    var QUALITY = 0.85;       // JPEG kalitesi
    var SKIP_BELOW = 1048576; // 1 MB altındakilere dokunma
    var MIN_PLAUSIBLE = 2048; // bundan küçük çıktı bozuk sayılır (boş tuval)

    function supported() {
        return typeof HTMLCanvasElement !== "undefined" &&
               typeof DataTransfer !== "undefined";
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
        try {
            var file = new File(
                [blob],
                originalName.replace(/\.[^.]+$/, "") + ".jpg",
                { type: "image/jpeg", lastModified: Date.now() });

            var transfer = new DataTransfer();
            transfer.items.add(file);
            input.files = transfer.files;

            showNote(
                input,
                "Fotoğraf otomatik küçültüldü: " +
                formatSize(originalSize) + " → " + formatSize(file.size));
        } catch (e) {
            // Dosya değiştirilemedi: orijinal gönderilir, sunucu karar verir.
        }
    }

    /// Kaynağı ölçekleyip JPEG blob üretir. Başarısızlıkta null döner.
    function toScaledBlob(source, width, height, done) {
        var scale = Math.min(1, MAX_EDGE / Math.max(width, height));

        var canvas = document.createElement("canvas");
        canvas.width = Math.max(1, Math.round(width * scale));
        canvas.height = Math.max(1, Math.round(height * scale));

        var ctx = canvas.getContext("2d");

        if (!ctx) {
            done(null);
            return;
        }

        // Şeffaf PNG'ler JPEG'e siyah dönmesin.
        ctx.fillStyle = "#ffffff";
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        try {
            ctx.drawImage(source, 0, 0, canvas.width, canvas.height);
        } catch (e) {
            done(null);
            return;
        }

        canvas.toBlob(function (blob) { done(blob); }, "image/jpeg", QUALITY);
    }

    function finish(input, blob, originalName, originalSize) {
        // Bozuk/boş tuval küçük bir blob üretebilir; bu durumda orijinali koru.
        if (!blob || blob.size < MIN_PLAUSIBLE || blob.size >= originalSize) {
            return;
        }

        replaceFile(input, blob, originalName, originalSize);
    }

    function viaImageBitmap(file, input) {
        // createImageBitmap bellek açısından daha verimli ve daha çok format
        // çözüyor; büyük fotoğraflarda <img> yolundan güvenilir.
        createImageBitmap(file).then(function (bitmap) {
            toScaledBlob(bitmap, bitmap.width, bitmap.height, function (blob) {
                finish(input, blob, file.name, file.size);
                if (bitmap.close) { bitmap.close(); }
            });
        }).catch(function () {
            viaImageElement(file, input);
        });
    }

    function viaImageElement(file, input) {
        var url = URL.createObjectURL(file);
        var img = new Image();

        img.onload = function () {
            toScaledBlob(img, img.naturalWidth, img.naturalHeight, function (blob) {
                finish(input, blob, file.name, file.size);
                URL.revokeObjectURL(url);
            });
        };

        // Çözülemeyen format (ör. HEIC): orijinal gönderilir.
        img.onerror = function () { URL.revokeObjectURL(url); };

        img.src = url;
    }

    function handle(input) {
        var file = input.files && input.files[0];

        if (!file || file.type.indexOf("image/") !== 0 || file.size < SKIP_BELOW) {
            return;
        }

        if (typeof createImageBitmap === "function") {
            viaImageBitmap(file, input);
        } else {
            viaImageElement(file, input);
        }
    }

    function attach() {
        if (!supported()) {
            return;
        }

        Array.prototype.forEach.call(
            document.querySelectorAll('input[type="file"][data-downscale]'),
            function (input) {
                input.addEventListener("change", function () { handle(input); });
            });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", attach);
    } else {
        attach();
    }
})();
