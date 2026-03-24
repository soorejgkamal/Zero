// Service Worker for Zero Card Game PWA
const CACHE_NAME = 'zero-pwa-v1';
const OFFLINE_URL = '/offline.html';

// Assets to pre-cache for a faster shell load
const PRECACHE_ASSETS = [
    '/',
    '/offline.html',
    '/css/app.css',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/icon-192.png',
    '/icon-512.png',
    '/favicon.png'
];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => cache.addAll(PRECACHE_ASSETS))
    );
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((keys) =>
            Promise.all(
                keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key))
            )
        )
    );
    self.clients.claim();
});

self.addEventListener('fetch', (event) => {
    // Only handle GET requests for same-origin URLs
    if (event.request.method !== 'GET' || !event.request.url.startsWith(self.location.origin)) {
        return;
    }

    // Skip SignalR negotiate/WebSocket requests – always go to network
    if (event.request.url.includes('/_blazor') || event.request.url.includes('/negotiate')) {
        return;
    }

    event.respondWith(
        fetch(event.request)
            .then((response) => {
                // Cache successful navigation responses
                if (event.request.mode === 'navigate' && response.ok) {
                    const responseClone = response.clone();
                    caches.open(CACHE_NAME).then((cache) => cache.put(event.request, responseClone));
                }
                return response;
            })
            .catch(() => {
                // Offline fallback: serve cached page or the offline page
                if (event.request.mode === 'navigate') {
                    return caches.match(event.request).then(
                        (cached) => cached || caches.match(OFFLINE_URL)
                    );
                }
                return caches.match(event.request);
            })
    );
});
