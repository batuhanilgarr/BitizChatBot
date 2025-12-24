(function (w, d) {
    'use strict';
    
    // Sadece desktop cihazlarda yüklensin (mobilde tamamen kapalı)
    if (window.innerWidth <= 768) {
        return;
    }

    // Zaten yüklenmişse tekrar yükleme
    if (w.BridgestoneChatbotLoaded) {
        return;
    }

    w.BridgestoneChatbotLoaded = true;

    // Configuration
    var config = {
        baseUrl: w.BridgestoneChatbotConfig?.baseUrl || 'https://chatdemo.bridgestone.com.tr',
        apiKey: w.BridgestoneChatbotConfig?.apiKey || '',
        domain: w.BridgestoneChatbotConfig?.domain || w.location.hostname,
        delay: w.BridgestoneChatbotConfig?.delay || 2000,
        mode: w.BridgestoneChatbotConfig?.mode || 'iframe',
        popupWidth: w.BridgestoneChatbotConfig?.popupWidth || 420,
        popupHeight: w.BridgestoneChatbotConfig?.popupHeight || 720
    };

    // Domain'i normalize et
    function normalizeDomain(domain) {
        domain = domain.trim();
        if (domain.startsWith('http://')) domain = domain.substring(7);
        if (domain.startsWith('https://')) domain = domain.substring(8);
        if (domain.startsWith('www.')) domain = domain.substring(4);
        var slashIndex = domain.indexOf('/');
        if (slashIndex >= 0) domain = domain.substring(0, slashIndex);
        return domain.toLowerCase();
    }

    // API key ve domain kontrolü
    if (!config.apiKey) {
        console.warn('Bridgestone Chatbot: API key is required. Please set BridgestoneChatbotConfig.apiKey');
        return;
    }

    var normalizedDomain = normalizeDomain(config.domain);

    function getEmbedUrl() {
        return config.baseUrl + '/embed?apiKey=' + encodeURIComponent(config.apiKey) + '&domain=' + encodeURIComponent(normalizedDomain);
    }

    // Iframe oluştur
    function createIframe() {
        var iframe = d.createElement('iframe');
        iframe.id = 'bridgestone-chatbot-iframe';
        iframe.src = getEmbedUrl();
        iframe.style.cssText = 'position:fixed;bottom:20px;right:20px;width:372px;height:714px;max-width:calc(100vw - 40px);max-height:calc(100vh - 40px);border:none;border-radius:16px;box-shadow:0 8px 36px rgba(0,18,46,0.16);z-index:9999;background:transparent;';
        iframe.setAttribute('allow', 'microphone; camera');
        iframe.setAttribute('frameborder', '0');
        iframe.setAttribute('scrolling', 'no');
        
        // Responsive için
        if (window.innerWidth <= 768) {
            iframe.style.width = 'calc(100vw - 20px)';
            iframe.style.height = 'calc(100vh - 20px)';
            iframe.style.bottom = '10px';
            iframe.style.right = '10px';
        }

        d.body.appendChild(iframe);
        
        // Iframe yüklendiğinde
        iframe.onload = function() {
            var placeholder = d.getElementById('bridgestone-chatbot-placeholder');
            if (placeholder) {
                placeholder.style.opacity = '1';
            }
        };
    }

    function openPopup() {
        var width = Math.max(320, Number(config.popupWidth) || 420);
        var height = Math.max(480, Number(config.popupHeight) || 720);
        var left = Math.max(0, Math.round((screen.width - width) / 2));
        var top = Math.max(0, Math.round((screen.height - height) / 2));
        var features = 'width=' + width + ',height=' + height + ',left=' + left + ',top=' + top + ',resizable=yes,scrollbars=no';
        var win = w.open(getEmbedUrl(), 'bridgestone_chatbot', features);
        if (win) {
            try { win.focus(); } catch (e) {}
        } else {
            // Popup engellendiyse yeni sekme açmayı dene
            w.open(getEmbedUrl(), '_blank');
        }
    }

    function createLauncherButton() {
        // Zaten varsa tekrar oluşturma
        if (d.getElementById('bridgestone-chatbot-launcher')) {
            return;
        }

        var btn = d.createElement('button');
        btn.id = 'bridgestone-chatbot-launcher';
        btn.type = 'button';
        btn.setAttribute('aria-label', 'Bridgestone Chatbot');
        btn.textContent = '💬';
        btn.style.cssText = 'position:fixed;bottom:20px;right:20px;width:60px;height:60px;border-radius:28px;border:none;cursor:pointer;z-index:9999;background:linear-gradient(135deg,#660000 0%,#5B0000 100%);color:#fff;font-size:24px;box-shadow:0 8px 36px rgba(0,18,46,0.16);display:flex;align-items:center;justify-content:center;';

        btn.addEventListener('click', function () {
            if (config.mode === 'tab') {
                w.open(getEmbedUrl(), '_blank');
                return;
            }
            openPopup();
        });

        d.body.appendChild(btn);
    }

    // Sayfa yüklendiğinde embed oluştur
    if (d.readyState === 'loading') {
        d.addEventListener('DOMContentLoaded', function() {
            setTimeout(function () {
                if (config.mode === 'iframe') {
                    createIframe();
                } else {
                    createLauncherButton();
                }
            }, config.delay);
        });
    } else {
        setTimeout(function () {
            if (config.mode === 'iframe') {
                createIframe();
            } else {
                createLauncherButton();
            }
        }, config.delay);
    }

    // Window resize için responsive ayarları
    w.addEventListener('resize', function() {
        if (config.mode !== 'iframe') {
            return;
        }
        var iframe = d.getElementById('bridgestone-chatbot-iframe');
        if (iframe) {
            if (window.innerWidth <= 768) {
                iframe.style.width = 'calc(100vw - 20px)';
                iframe.style.height = 'calc(100vh - 20px)';
                iframe.style.bottom = '10px';
                iframe.style.right = '10px';
            } else {
                iframe.style.width = '372px';
                iframe.style.height = '714px';
                iframe.style.bottom = '20px';
                iframe.style.right = '20px';
            }
        }
    });

})(window, document);

