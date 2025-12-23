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

// Enable drag-to-scroll for horizontal card containers
window.enableDragScroll = (selector) => {
    const elements = document.querySelectorAll(selector);
    elements.forEach((el) => {
        let isDown = false;
        let startX = 0;
        let scrollLeft = 0;

        el.addEventListener('mousedown', (e) => {
            isDown = true;
            el.classList.add('dragging');
            startX = e.pageX - el.offsetLeft;
            scrollLeft = el.scrollLeft;
        });

        el.addEventListener('mouseleave', () => {
            isDown = false;
            el.classList.remove('dragging');
        });

        el.addEventListener('mouseup', () => {
            isDown = false;
            el.classList.remove('dragging');
        });

        el.addEventListener('mousemove', (e) => {
            if (!isDown) return;
            e.preventDefault();
            const x = e.pageX - el.offsetLeft;
            const walk = x - startX;
            el.scrollLeft = scrollLeft - walk;
        });
    });
};

// LocalStorage functions for session persistence
window.saveChatSession = (sessionId, messages) => {
    try {
        localStorage.setItem('chatbot_sessionId', sessionId);
        localStorage.setItem('chatbot_messages', JSON.stringify(messages));
    } catch (e) {
        console.error('Error saving chat session:', e);
    }
};

window.loadChatSession = () => {
    try {
        const sessionId = localStorage.getItem('chatbot_sessionId');
        const messagesJson = localStorage.getItem('chatbot_messages');
        const messages = messagesJson ? JSON.parse(messagesJson) : null;
        return { sessionId, messages };
    } catch (e) {
        console.error('Error loading chat session:', e);
        return { sessionId: null, messages: null };
    }
};

window.clearChatSession = () => {
    try {
        localStorage.removeItem('chatbot_sessionId');
        localStorage.removeItem('chatbot_messages');
    } catch (e) {
        console.error('Error clearing chat session:', e);
    }
};

// Download file function
window.downloadFile = (filename, content, contentType) => {
    const element = document.createElement('a');
    const file = new Blob([content], { type: contentType || 'text/plain;charset=utf-8' });
    element.href = URL.createObjectURL(file);
    element.download = filename;
    document.body.appendChild(element);
    element.click();
    document.body.removeChild(element);
    URL.revokeObjectURL(element.href);
};

