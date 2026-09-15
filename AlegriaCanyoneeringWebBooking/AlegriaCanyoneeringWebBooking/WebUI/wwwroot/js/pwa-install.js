/**
 * Alegria Canyoneering Web Booking - Universal PWA Installer with Progress Bar
 */
(function () {
  let deferredPrompt = null;
  const isStandalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;

  function showAllInstallUI() {
    if (isStandalone) return;
    document.querySelectorAll('#pwaInstallBanner, .pwa-install-banner').forEach(b => {
      b.style.display = 'flex';
      b.style.visibility = 'visible';
    });
    document.querySelectorAll('#pwaInstallBtn, .btn-pwa-install, #pwaNavInstallBtn, #pwaMobileInstallBtn').forEach(b => {
      b.style.display = 'inline-flex';
    });
  }

  function hideAllInstallUI() {
    document.querySelectorAll('#pwaInstallBanner, .pwa-install-banner, #pwaInstallBtn, .btn-pwa-install, #pwaNavInstallBtn, #pwaMobileInstallBtn').forEach(el => {
      el.style.display = 'none';
    });
  }

  if (isStandalone) {
    hideAllInstallUI();
    return;
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', showAllInstallUI);
  } else {
    showAllInstallUI();
  }

  // Capture beforeinstallprompt
  window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferredPrompt = e;
    console.log('[PWA] Native install prompt captured.');
    showAllInstallUI();
  });

  window.addEventListener('appinstalled', () => {
    console.log('[PWA] App successfully installed.');
    deferredPrompt = null;
    hideAllInstallUI();
    const modal = document.getElementById('pwaSimpleDownloadModal');
    if (modal) modal.style.display = 'none';
  });

  // Windows Desktop Shortcut (.url) fallback
  function downloadDesktopShortcut() {
    const urlContent = `[InternetShortcut]\r\nURL=${window.location.origin}/\r\nIconIndex=0\r\nIconFile=${window.location.origin}/favicon.ico\r\n`;
    const blob = new Blob([urlContent], { type: 'application/x-mswinurl' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'Alegria Canyoneering Booking.url';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(a.href);
  }

  function openDownloadModal() {
    let modal = document.getElementById('pwaSimpleDownloadModal');
    if (!modal) {
      modal = document.createElement('div');
      modal.id = 'pwaSimpleDownloadModal';
      modal.innerHTML = `
        <div class="pwa-download-overlay" id="pwaDownloadOverlay">
          <div class="pwa-download-card" role="dialog" aria-modal="true">
            <button class="pwa-download-close" id="pwaDownloadCloseBtn" aria-label="Close">&times;</button>
            
            <div class="pwa-download-body">
              <img src="/images/icon-192.png" alt="Alegria App" class="pwa-app-icon" />
              <h3 class="pwa-download-title">Alegria Canyoneering</h3>
              <p class="pwa-download-desc">Install official web app for fast access and offline booking.</p>

              <!-- State 1: Install Action Button -->
              <div id="pwaInitialState">
                <button class="pwa-primary-download-btn" id="pwaMainActionBtn">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                    <polyline points="7 10 12 15 17 10"></polyline>
                    <line x1="12" y1="15" x2="12" y2="3"></line>
                  </svg>
                  <span>Download & Install App</span>
                </button>
              </div>

              <!-- State 2: Animated Progress Bar -->
              <div id="pwaProgressContainer" style="display: none;">
                <div class="pwa-progress-header">
                  <span id="pwaProgressStatus">Preparing installation...</span>
                  <span id="pwaPercentText" class="pwa-percent-badge">0%</span>
                </div>
                
                <div class="pwa-progress-track">
                  <div class="pwa-progress-bar" id="pwaProgressBar"></div>
                </div>

                <div class="pwa-progress-substatus" id="pwaSubStatus">
                  Downloading cached assets...
                </div>
              </div>

              <!-- State 3: Completed Prompt Box -->
              <div id="pwaCompletedBox" style="display: none;">
                <div class="pwa-success-badge">
                  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="#16a34a" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
                    <polyline points="20 6 9 17 4 12"></polyline>
                  </svg>
                  <div>
                    <strong>Ready to Install! (100%)</strong>
                    <p>Click <strong>Install</strong> in the browser prompt or address bar icon <span class="pwa-icon-pill">&#10515;</span> to complete.</p>
                  </div>
                </div>

                <button class="pwa-primary-download-btn" id="pwaPromptTriggerBtn" style="margin-top: 1rem;">
                  <span>Open Install Prompt</span>
                </button>
              </div>

              <!-- Desktop Shortcut Fallback -->
              <div class="pwa-alt-section">
                <button class="pwa-secondary-btn" id="pwaShortcutBtn">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M9 3v18"/><path d="M14 9l3 3-3 3"/></svg>
                  <span>Or Save Desktop Shortcut Icon</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      `;

      // Inject Styles
      const style = document.createElement('style');
      style.textContent = `
        .pwa-download-overlay {
          position: fixed;
          top: 0; left: 0; right: 0; bottom: 0;
          background: rgba(15, 23, 42, 0.72);
          backdrop-filter: blur(8px);
          -webkit-backdrop-filter: blur(8px);
          z-index: 999999;
          display: flex;
          align-items: center;
          justify-content: center;
          padding: 1rem;
          box-sizing: border-box;
          animation: pwaFadeIn 0.2s ease-out;
        }
        @keyframes pwaFadeIn {
          from { opacity: 0; }
          to { opacity: 1; }
        }
        .pwa-download-card {
          background: #ffffff;
          color: #0f172a;
          width: 100%;
          max-width: 440px;
          border-radius: 24px;
          box-shadow: 0 25px 60px -12px rgba(0, 0, 0, 0.4);
          padding: 2rem 1.75rem;
          position: relative;
          text-align: center;
          box-sizing: border-box;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
          animation: pwaScaleUp 0.25s cubic-bezier(0.16, 1, 0.3, 1);
        }
        @keyframes pwaScaleUp {
          from { transform: scale(0.92); opacity: 0; }
          to { transform: scale(1); opacity: 1; }
        }
        .pwa-download-close {
          position: absolute;
          top: 1rem;
          right: 1rem;
          background: #f1f5f9;
          border: none;
          font-size: 1.4rem;
          width: 32px;
          height: 32px;
          border-radius: 50%;
          cursor: pointer;
          line-height: 1;
          color: #64748b;
          display: flex;
          align-items: center;
          justify-content: center;
          transition: all 0.2s;
        }
        .pwa-download-close:hover {
          background: #e2e8f0;
          color: #0f172a;
        }
        .pwa-app-icon {
          width: 76px;
          height: 76px;
          border-radius: 18px;
          box-shadow: 0 10px 25px -5px rgba(15, 52, 96, 0.25);
          object-fit: cover;
          margin-bottom: 1rem;
        }
        .pwa-download-title {
          margin: 0 0 0.4rem;
          font-size: 1.35rem;
          font-weight: 700;
          color: #0f3460;
        }
        .pwa-download-desc {
          margin: 0 0 1.25rem;
          font-size: 0.9rem;
          color: #475569;
          line-height: 1.4;
        }
        .pwa-primary-download-btn {
          width: 100%;
          background: linear-gradient(135deg, #0f3460 0%, #1a6ef5 100%);
          color: #ffffff;
          border: none;
          padding: 0.85rem 1.25rem;
          border-radius: 14px;
          font-weight: 700;
          font-size: 1rem;
          cursor: pointer;
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 0.6rem;
          box-shadow: 0 8px 20px -4px rgba(26, 110, 245, 0.4);
          transition: transform 0.15s, box-shadow 0.15s;
        }
        .pwa-primary-download-btn:hover {
          transform: translateY(-2px);
          box-shadow: 0 12px 26px -4px rgba(26, 110, 245, 0.5);
        }

        /* Progress Bar Styles */
        .pwa-progress-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 0.5rem;
          font-size: 0.85rem;
          font-weight: 600;
          color: #334155;
        }
        .pwa-percent-badge {
          background: #eff6ff;
          color: #1a6ef5;
          padding: 2px 8px;
          border-radius: 999px;
          font-weight: 700;
          font-size: 0.88rem;
        }
        .pwa-progress-track {
          width: 100%;
          height: 12px;
          background: #e2e8f0;
          border-radius: 999px;
          overflow: hidden;
          position: relative;
          box-shadow: inset 0 1px 3px rgba(0,0,0,0.1);
        }
        .pwa-progress-bar {
          width: 0%;
          height: 100%;
          background: linear-gradient(90deg, #1a6ef5, #00d2ff);
          border-radius: 999px;
          transition: width 0.12s ease-out;
        }
        .pwa-progress-substatus {
          margin-top: 0.65rem;
          font-size: 0.8rem;
          color: #64748b;
          text-align: center;
        }

        /* Success Box */
        .pwa-success-badge {
          background: #f0fdf4;
          border: 1.5px solid #86efac;
          border-radius: 14px;
          padding: 0.85rem 1rem;
          text-align: left;
          display: flex;
          align-items: center;
          gap: 0.85rem;
        }
        .pwa-success-badge strong {
          color: #166534;
          font-size: 0.95rem;
          display: block;
          margin-bottom: 0.2rem;
        }
        .pwa-success-badge p {
          margin: 0;
          font-size: 0.82rem;
          color: #15803d;
          line-height: 1.4;
        }
        .pwa-icon-pill {
          background: #16a34a;
          color: white;
          padding: 1px 6px;
          border-radius: 6px;
          font-weight: 700;
        }

        .pwa-alt-section {
          border-top: 1px solid #f1f5f9;
          margin-top: 1.25rem;
          padding-top: 1rem;
        }
        .pwa-secondary-btn {
          width: 100%;
          background: #f8fafc;
          border: 1.5px solid #e2e8f0;
          color: #334155;
          padding: 0.65rem 1rem;
          border-radius: 12px;
          font-weight: 600;
          font-size: 0.85rem;
          cursor: pointer;
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 0.5rem;
          transition: all 0.2s;
        }
        .pwa-secondary-btn:hover {
          background: #f1f5f9;
          border-color: #cbd5e1;
          color: #0f172a;
        }
      `;
      document.head.appendChild(style);
      document.body.appendChild(modal);

      // Close handler
      const close = () => { modal.style.display = 'none'; };
      document.getElementById('pwaDownloadCloseBtn').addEventListener('click', close);
      document.getElementById('pwaDownloadOverlay').addEventListener('click', (e) => {
        if (e.target.id === 'pwaDownloadOverlay') close();
      });

      // Simulation Function with realistic stages and percent update
      function startProgressBar() {
        const initialState = document.getElementById('pwaInitialState');
        const progressContainer = document.getElementById('pwaProgressContainer');
        const completedBox = document.getElementById('pwaCompletedBox');
        const progressBar = document.getElementById('pwaProgressBar');
        const percentText = document.getElementById('pwaPercentText');
        const progressStatus = document.getElementById('pwaProgressStatus');
        const subStatus = document.getElementById('pwaSubStatus');

        initialState.style.display = 'none';
        progressContainer.style.display = 'block';
        completedBox.style.display = 'none';

        let percent = 0;

        const stages = [
          { at: 20, status: 'Downloading assets...', sub: 'Caching app icons and layout styles' },
          { at: 50, status: 'Setting up offline database...', sub: 'Configuring cache trust storage' },
          { at: 80, status: 'Registering service worker...', sub: 'Preparing background sync' },
          { at: 95, status: 'Finalizing installation...', sub: 'Registering app launcher' },
          { at: 100, status: 'Completed!', sub: 'Installation ready' }
        ];

        const interval = setInterval(() => {
          // Increment percent smoothly
          const step = percent < 60 ? Math.floor(Math.random() * 8) + 4 : Math.floor(Math.random() * 5) + 3;
          percent = Math.min(100, percent + step);

          progressBar.style.width = percent + '%';
          percentText.textContent = percent + '%';

          // Update stage messages
          for (let i = 0; i < stages.length; i++) {
            if (percent >= stages[i].at) {
              progressStatus.textContent = stages[i].status;
              subStatus.textContent = stages[i].sub;
            }
          }

          if (percent >= 100) {
            clearInterval(interval);
            setTimeout(() => {
              progressContainer.style.display = 'none';
              completedBox.style.display = 'block';

              // Automatically trigger native prompt if available
              if (deferredPrompt) {
                try {
                  deferredPrompt.prompt();
                  deferredPrompt.userChoice.then(({ outcome }) => {
                    if (outcome === 'accepted') {
                      deferredPrompt = null;
                      hideAllInstallUI();
                      close();
                    }
                  });
                } catch (err) {
                  console.log('[PWA] Prompt trigger:', err);
                }
              }
            }, 400);
          }
        }, 120);
      }

      // Start progress when user clicks Main Download button
      document.getElementById('pwaMainActionBtn').addEventListener('click', () => {
        startProgressBar();
      });

      // When in completed state, trigger prompt button
      document.getElementById('pwaPromptTriggerBtn').addEventListener('click', async () => {
        if (deferredPrompt) {
          try {
            deferredPrompt.prompt();
            const { outcome } = await deferredPrompt.userChoice;
            if (outcome === 'accepted') {
              deferredPrompt = null;
              hideAllInstallUI();
              close();
            }
          } catch (err) {
            console.error(err);
          }
        }
      });

      // Shortcut button
      document.getElementById('pwaShortcutBtn').addEventListener('click', () => {
        downloadDesktopShortcut();
        close();
      });
    }

    modal.style.display = 'block';
  }

  // Handle click on any install/download button anywhere on the page
  document.addEventListener('click', (e) => {
    const target = e.target.closest('#pwaInstallBtn, .btn-pwa-install, #pwaNavInstallBtn, #pwaMobileInstallBtn');
    if (target) {
      e.preventDefault();
      openDownloadModal();
    }
  });

  // Service Worker Registration
  if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
      navigator.serviceWorker.register('/service-worker.js')
        .then(reg => {
          console.log('[SW] Registered, scope:', reg.scope);
          reg.update();
        })
        .catch(err => console.error('[SW] Registration failed:', err));
    });
  }
})();
