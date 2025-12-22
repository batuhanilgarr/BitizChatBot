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
        delay: w.BridgestoneChatbotConfig?.delay || 2000
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

    // Iframe oluştur
    function createIframe() {
        var iframe = d.createElement('iframe');
        iframe.id = 'bridgestone-chatbot-iframe';
        iframe.src = config.baseUrl + '/embed?apiKey=' + encodeURIComponent(config.apiKey) + '&domain=' + encodeURIComponent(normalizedDomain);
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

    // Sayfa yüklendiğinde iframe oluştur
    if (d.readyState === 'loading') {
        d.addEventListener('DOMContentLoaded', function() {
            setTimeout(createIframe, config.delay);
        });
    } else {
        setTimeout(createIframe, config.delay);
    }

    // Window resize için responsive ayarları
    w.addEventListener('resize', function() {
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

