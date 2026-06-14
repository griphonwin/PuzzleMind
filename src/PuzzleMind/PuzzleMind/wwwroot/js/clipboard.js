export async function getImageFromClipboard() {
    try {
        // Запрашиваем разрешение явно (не обязательно, но помогает)
        const permission = await navigator.permissions.query({ name: "clipboard-read" });
        if (permission.state === "denied") {
            throw new Error("Доступ к буферу запрещен в настройках браузера");
        }

        const items = await navigator.clipboard.read();
        for (const item of items) {
            if (item.types.includes('image/png')) {
                const blob = await item.getType('image/png');
                const buffer = await blob.arrayBuffer();
                return new Uint8Array(buffer);
            }
        }
    } catch (err) {
        alert("Ошибка буфера: " + err.message); // Выведет окно в браузере
        return null;
    }
}