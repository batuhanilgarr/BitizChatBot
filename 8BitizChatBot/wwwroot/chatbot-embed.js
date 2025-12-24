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
        // openOnLoad will be pulled from server (/embed-config) so it doesn't need to be in the embed snippet
        openOnLoad: w.BridgestoneChatbotConfig?.openOnLoad !== undefined ? !!w.BridgestoneChatbotConfig.openOnLoad : undefined,
        popupWidth: w.BridgestoneChatbotConfig?.popupWidth || 420,
        popupHeight: w.BridgestoneChatbotConfig?.popupHeight || 720
    };

    // API key ve domain kontrolü
    if (!config.apiKey) {
        console.warn('Bridgestone Chatbot: API key is required. Please set BridgestoneChatbotConfig.apiKey');
        return;
    }

    function getEmbedUrl() {
        // baseUrl'den trailing slash'i temizle
        var baseUrl = config.baseUrl.replace(/\/$/, '');
        // Path base kontrolü - eğer baseUrl'de /chatbot yoksa ekle
        if (!baseUrl.includes('/chatbot')) {
            baseUrl += '/chatbot';
        }
        var url = baseUrl + '/embed?apiKey=' + encodeURIComponent(config.apiKey);
        // Domain parametresini ekle
        if (config.domain) {
            url += '&domain=' + encodeURIComponent(config.domain);
        }
        return url;
    }

    function fetchEmbedConfig() {
        // If host already set openOnLoad explicitly, don't fetch
        if (config.openOnLoad !== undefined) {
            return Promise.resolve({ openOnLoad: !!config.openOnLoad });
        }

        // Same-origin not required; server responds with Access-Control-Allow-Origin for validated Origin.
        // baseUrl'den trailing slash'i temizle ve /chatbot path'ini ekle
        var baseUrl = config.baseUrl.replace(/\/$/, '');
        if (!baseUrl.includes('/chatbot')) {
            baseUrl += '/chatbot';
        }
        var url = baseUrl + '/embed-config?apiKey=' + encodeURIComponent(config.apiKey);
        return fetch(url, { method: 'GET', credentials: 'omit' })
            .then(function (res) {
                if (!res.ok) {
                    throw new Error('embed-config not ok');
                }
                return res.json();
            })
            .then(function (json) {
                if (typeof json.openOnLoad === 'boolean') {
                    config.openOnLoad = json.openOnLoad;
                } else {
                    config.openOnLoad = true;
                }
                return json;
            })
            .catch(function () {
                // safest default: do not auto-open
                config.openOnLoad = false;
                return { openOnLoad: false };
            });
    }

    function getIframe() {
        return d.getElementById('bridgestone-chatbot-iframe');
    }

    function showLauncher(show) {
        var btn = d.getElementById('bridgestone-chatbot-launcher');
        if (!btn) return;
        btn.style.display = show ? 'flex' : 'none';
    }

    // Iframe oluştur
    function createIframe() {
        var existing = getIframe();
        if (existing) {
            return existing;
        }
        var iframe = d.createElement('iframe');
        iframe.id = 'bridgestone-chatbot-iframe';
        iframe.src = getEmbedUrl();
        iframe.style.cssText = 'position:fixed;bottom:20px;right:20px;width:100%;height:100%;max-width:calc(100vw - 40px);max-height:calc(100vh - 40px);border:none;border-radius:16px;box-shadow:0 8px 36px rgba(0,18,46,0.16);z-index:9999;background:transparent;';
        iframe.setAttribute('allow', 'microphone; camera; geolocation');
        iframe.setAttribute('frameborder', '0');
        iframe.setAttribute('scrolling', 'no');
        

        d.body.appendChild(iframe);
        
        // Iframe yüklendiğinde
        iframe.onload = function() {
            var placeholder = d.getElementById('bridgestone-chatbot-placeholder');
            if (placeholder) {
                placeholder.style.opacity = '1';
            }
            showLauncher(false);
        };

        return iframe;
    }

    function removeIframe() {
        var iframe = getIframe();
        if (iframe && iframe.parentNode) {
            iframe.parentNode.removeChild(iframe);
        }
        showLauncher(true);
    }

    function openPopup() {
        var width = Math.max(320, Number(config.popupWidth) || 420);
        var height = Math.max(480, Number(config.popupHeight) || 720);
        var left = Math.max(0, Math.round((screen.width - width) / 2));
        var top = Math.max(0, Math.round((screen.height - height) / 2));
        var features = 'resizable=yes,scrollbars=no';
        var win = w.open(getEmbedUrl(), 'bridgestone_chatbot', features);
        if (win) {
            try { win.focus(); } catch (e) {}
        } else {
            // Popup engellendiyse yeni sekme açmayı dene
            w.open(getEmbedUrl(), '_blank');
        }
    }

    function loadFontAwesome() {
        // Check if FontAwesome is already loaded
        if (d.querySelector('link[href*="font-awesome"]') || d.querySelector('link[href*="fontawesome"]')) {
            return Promise.resolve();
        }
        
        return new Promise(function(resolve, reject) {
            var link = d.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.1/css/all.min.css';
            link.integrity = 'sha512-DTOQO9RWCH3ppGqcWaEA1BIZOC6xxalwEsw9c2QQeAIftl+Vegovlnee1c9QX4TctnWMn13TZye+giMm8e2LwA==';
            link.crossOrigin = 'anonymous';
            link.referrerPolicy = 'no-referrer';
            link.onload = function() { resolve(); };
            link.onerror = function() { reject(); };
            d.head.appendChild(link);
        });
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
        btn.innerHTML = '<i class="fa-solid fa-comments"></i>';
        btn.style.cssText = 'position:fixed;bottom:20px;right:20px;width:60px;height:60px;border-radius:28px;border:none;cursor:pointer;z-index:9999;background:linear-gradient(135deg,#660000 0%,#5B0000 100%);color:#fff;font-size:24px;box-shadow:0 8px 36px rgba(0,18,46,0.16);display:flex;align-items:center;justify-content:center;';

        btn.addEventListener('click', function () {
            if (config.mode === 'tab') {
                w.open(getEmbedUrl(), '_blank');
                return;
            }
            if (config.mode === 'popup') {
                openPopup();
                return;
            }
            // iframe mode: toggle open/close
            if (getIframe()) {
                removeIframe();
            } else {
                createIframe();
            }
        });

        d.body.appendChild(btn);
    }

    // Sayfa yüklendiğinde embed oluştur
    if (d.readyState === 'loading') {
        d.addEventListener('DOMContentLoaded', function() {
            setTimeout(function () {
                loadFontAwesome().then(function() {
                    createLauncherButton();
                    fetchEmbedConfig().then(function () {
                        if (config.mode === 'iframe' && config.openOnLoad) {
                            createIframe();
                        }
                    });
                }).catch(function() {
                    // If FontAwesome fails to load, use emoji fallback
                    var btn = d.getElementById('bridgestone-chatbot-launcher');
                    if (btn) {
                        btn.textContent = '💬';
                    }
                    createLauncherButton();
                    fetchEmbedConfig().then(function () {
                        if (config.mode === 'iframe' && config.openOnLoad) {
                            createIframe();
                        }
                    });
                });
            }, config.delay);
        });
    } else {
        setTimeout(function () {
            loadFontAwesome().then(function() {
                createLauncherButton();
                fetchEmbedConfig().then(function () {
                    if (config.mode === 'iframe' && config.openOnLoad) {
                        createIframe();
                    }
                });
            }).catch(function() {
                // If FontAwesome fails to load, use emoji fallback
                var btn = d.getElementById('bridgestone-chatbot-launcher');
                if (btn) {
                    btn.textContent = '💬';
                }
                createLauncherButton();
                fetchEmbedConfig().then(function () {
                    if (config.mode === 'iframe' && config.openOnLoad) {
                        createIframe();
                    }
                });
            });
        }, config.delay);
    }


})(window, document);

