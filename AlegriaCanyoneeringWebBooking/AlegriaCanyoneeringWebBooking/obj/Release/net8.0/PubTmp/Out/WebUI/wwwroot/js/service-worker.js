const CACHE_NAME = 'alegria-pwa-v4';

// Core essential assets to pre-cache on install
const PRECACHE_ASSETS = [
  '/',
  '/Authentication/Login',
  '/manifest.json',
  '/images/icon-192.png',
  '/images/icon-512.png',
  '/images/alegrialogo2025.jpeg',
  '/favicon.ico',
  '/js/pwa-install.js',
  '/css/site.css',
  '/lib/bootstrap/dist/css/bootstrap.min.css',
  '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
  '/lib/jquery/dist/jquery.min.js'
];

// 1. Install: Pre-cache core assets gracefully (don't fail install if an optional asset 404s)
self.addEventListener('install', event => {
  console.log('[SW] Installing Service Worker with trusted cache strategy...');
  event.waitUntil(
    caches.open(CACHE_NAME).then(async cache => {
      await Promise.allSettled(
        PRECACHE_ASSETS.map(url =>
          fetch(url, { cache: 'no-cache' })
            .then(res => {
              if (res.ok) {
                return cache.put(url, res);
              }
            })
            .catch(err => console.warn('[SW] Could not precache:', url, err.message))
        )
      );
      console.log('[SW] Core assets cached successfully.');
    }).then(() => self.skipWaiting())
  );
});

// 2. Activate: Clean up old cache versions immediately
self.addEventListener('activate', event => {
  console.log('[SW] Activating new Service Worker...');
  event.waitUntil(
    caches.keys().then(keys => {
      return Promise.all(
        keys.filter(key => key !== CACHE_NAME).map(key => {
          console.log('[SW] Deleting old cache:', key);
          return caches.delete(key);
        })
      );
    }).then(() => self.clients.claim())
  );
});

// 3. Fetch: Trusted Caching Strategy
self.addEventListener('fetch', event => {
  const req = event.request;

  // Only handle GET requests with http/https schemes
  if (req.method !== 'GET') return;
  if (!req.url.startsWith('http://') && !req.url.startsWith('https://')) return;

  const url = new URL(req.url);

  // A. Static Assets (images, css, js, fonts): Cache-First / Stale-While-Revalidate
  // Trust the cached version immediately for speed & reliability, while refreshing in background
  const isStaticAsset = (
    url.pathname.startsWith('/css/') ||
    url.pathname.startsWith('/js/') ||
    url.pathname.startsWith('/images/') ||
    url.pathname.startsWith('/lib/') ||
    url.pathname === '/manifest.json' ||
    url.pathname === '/favicon.ico' ||
    req.destination === 'style' ||
    req.destination === 'script' ||
    req.destination === 'image' ||
    req.destination === 'font'
  );

  if (isStaticAsset) {
    event.respondWith(
      caches.match(req).then(cachedResponse => {
        const fetchAndCache = fetch(req).then(networkResponse => {
          if (networkResponse && (networkResponse.status === 200 || networkResponse.type === 'opaque')) {
            const copy = networkResponse.clone();
            caches.open(CACHE_NAME).then(cache => cache.put(req, copy));
          }
          return networkResponse;
        }).catch(() => cachedResponse);

        // Return trusted cache immediately if found, otherwise wait for network
        return cachedResponse || fetchAndCache;
      })
    );
    return;
  }

  // B. HTML Navigations / Pages: Network-First with trusted Cache Fallback
  if (req.mode === 'navigate' || req.headers.get('accept')?.includes('text/html')) {
    event.respondWith(
      fetch(req).then(networkResponse => {
        if (networkResponse && networkResponse.status === 200) {
          const copy = networkResponse.clone();
          caches.open(CACHE_NAME).then(cache => cache.put(req, copy));
        }
        return networkResponse;
      }).catch(async () => {
        // Network failed / offline: trust and serve cached page
        const cachedResponse = await caches.match(req);
        if (cachedResponse) return cachedResponse;

        // Fallback to cached home/landing or login page
        const fallbackHome = await caches.match('/');
        if (fallbackHome) return fallbackHome;

        const fallbackLogin = await caches.match('/Authentication/Login');
        if (fallbackLogin) return fallbackLogin;

        return new Response('<h1>Offline</h1><p>Please check your internet connection.</p>', {
          headers: { 'Content-Type': 'text/html' }
        });
      })
    );
    return;
  }

  // C. All other GET requests: Network first, trust cache fallback
  event.respondWith(
    fetch(req).then(networkResponse => {
      if (networkResponse && (networkResponse.status === 200 || networkResponse.type === 'opaque')) {
        const copy = networkResponse.clone();
        caches.open(CACHE_NAME).then(cache => cache.put(req, copy));
      }
      return networkResponse;
    }).catch(() => caches.match(req))
  );
});
