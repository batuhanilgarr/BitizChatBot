// Scroll to bottom of chat messages
window.scrollToBottom = (element) => {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

// Get current location
window.getCurrentLocation = () => {
    return new Promise((resolve) => {
        if (!navigator.geolocation) {
            resolve({ success: false, error: "Geolocation is not supported by this browser." });
            return;
        }

        navigator.geolocation.getCurrentPosition(
            (position) => {
                resolve({
                    success: true,
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude
                });
            },
            (error) => {
                resolve({
                    success: false,
                    error: error.message || "Unable to retrieve your location"
                });
            },
            {
                enableHighAccuracy: true,
                timeout: 10000,
                maximumAge: 0
            }
        );
    });
};

// Download text file
window.downloadTextFile = (filename, text) => {
    const element = document.createElement('a');
    const file = new Blob([text], { type: 'text/plain;charset=utf-8' });
    element.href = URL.createObjectURL(file);
    element.download = filename;
    document.body.appendChild(element);
    element.click();
    document.body.removeChild(element);
    URL.revokeObjectURL(element.href);
};

// Focus element
window.focusElement = (element) => {
    if (element) {
        element.focus();
    }
};

