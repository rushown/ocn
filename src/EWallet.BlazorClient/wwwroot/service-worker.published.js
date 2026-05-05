// Production service worker — handles offline caching for Blazor WASM
// This file is auto-processed by the Blazor WASM build pipeline.

const CACHE_NAME = 'ewallet-v1';

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME).then(cache =>
            cache.addAll(self.assetsManifest.assets
                .filter(a => a.url !== '/')
                .map(a => new Request(a.url, { integrity: a.hash }))
            )
        )
    );
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
        ).then(() => clients.claim())
    );
});

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;

    const url = new URL(event.request.url);

    // API calls — always network-first
    if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
        return;
    }

    event.respondWith(
        caches.match(event.request).then(cached => cached ?? fetch(event.request))
    );
});
