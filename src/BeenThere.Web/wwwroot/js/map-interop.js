// Minimal Leaflet interop module for BeenThere
const maps = new Map();

export function initMap(elementId, options = {}) {
  const el = document.getElementById(elementId);
  if (!el) return null;

  const center = (options.center && options.center.length === 2) ? options.center : [51.505, -0.09];
  const zoom = options.zoom ?? 13;
  const tileUrl = options.tileUrl ?? 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';

  const map = L.map(elementId).setView(center, zoom);
  L.tileLayer(tileUrl, { attribution: options.attribution || '&copy; OpenStreetMap contributors' }).addTo(map);

  const state = {
    map,
    routesLayer: L.layerGroup().addTo(map),
    clusterLayer: null,
    heatLayer: null,
    routeFeatures: new Map() // Store features by route ID for highlighting
  };

  maps.set(elementId, state);
  return true;
}

export function addRoutes(elementId, geojson) {
  const state = maps.get(elementId);
  if (!state) return;

  state.routesLayer.clearLayers();
  state.routeFeatures.clear();

  // Filter out features with null geometry
  const validFeatures = geojson.features?.filter(f => f.geometry) || [];
  console.log(`[map-interop] Rendering ${validFeatures.length} valid routes (of ${geojson.features?.length || 0})`);

  if (validFeatures.length === 0) {
    console.warn('[map-interop] No routes with geometry to render');
    return;
  }

  // Store features by ID
  validFeatures.forEach(feature => {
    if (feature.properties?.id) {
      state.routeFeatures.set(feature.properties.id, feature);
    }
  });

  const validGeoJson = { type: 'FeatureCollection', features: validFeatures };
  const gj = L.geoJSON(validGeoJson, {
    style: { color: '#0077cc', weight: 3 }
  });

  state.routesLayer.addLayer(gj);
  
  try {
    const bounds = gj.getBounds();
    if (bounds.isValid()) {
      state.map.fitBounds(bounds, { padding: [20, 20] });
    } else {
      console.warn('[map-interop] Invalid bounds; skipping fitBounds');
    }
  } catch (error) {
    console.warn('[map-interop] Error fitting bounds:', error);
  }
}

export async function loadRoutesFromUrl(elementId, url) {
  try {
    const res = await fetch(url, { credentials: 'same-origin' });
    if (!res.ok) {
      console.error(`[map-interop] Failed to load routes: ${res.status} ${res.statusText}`);
      return false;
    }
    const geojson = await res.json();
    console.log(`[map-interop] Loaded ${geojson.features?.length || 0} routes`);
    addRoutes(elementId, geojson);
    return true;
  } catch (error) {
    console.error('[map-interop] Error loading routes:', error);
    return false;
  }
}

export function hasRoutesLoaded(elementId) {
  const state = maps.get(elementId);
  if (!state || !state.routesLayer) return false;
  // Check if routesLayer has any layers (routes have been added)
  return state.routesLayer.getLayers().length > 0;
}

export async function waitForRoutes(elementId, maxWaitMs = 5000) {
  const startTime = performance.now();
  while (!hasRoutesLoaded(elementId)) {
    if (performance.now() - startTime > maxWaitMs) {
      console.warn('[map-interop] Timeout waiting for routes to load');
      return false;
    }
    await new Promise(resolve => setTimeout(resolve, 50));
  }
  console.log('[map-interop] Routes loaded and ready');
  return true;
}

export function highlightRoute(elementId, routeId) {
  const state = maps.get(elementId);
  if (!state) return;

  const feature = state.routeFeatures.get(routeId);
  if (!feature) {
    console.warn(`[map-interop] Route ${routeId} not found`);
    return;
  }

  // Remove previous highlight
  if (state.highlightLayer) {
    state.map.removeLayer(state.highlightLayer);
    state.highlightLayer = null;
  }

  // Create highlighted layer with distinct style
  const highlightGeoJson = L.geoJSON(feature, {
    style: { color: '#ff6b6b', weight: 5, opacity: 0.9 }
  });

  state.highlightLayer = highlightGeoJson;
  state.map.addLayer(highlightGeoJson);

  // Zoom to route
  try {
    const bounds = highlightGeoJson.getBounds();
    if (bounds.isValid()) {
      state.map.fitBounds(bounds, { padding: [50, 50] });
    }
  } catch (error) {
    console.warn('[map-interop] Error fitting to highlighted route:', error);
  }

  console.log(`[map-interop] Highlighted route ${routeId}`);
}

export function setTileProvider(elementId, tileUrl, attribution) {
  const state = maps.get(elementId);
  if (!state) return;
  // remove existing base layers by resetting and adding new tile layer
  state.map.eachLayer(function (layer) {
    if (layer instanceof L.TileLayer) state.map.removeLayer(layer);
  });
  L.tileLayer(tileUrl, { attribution: attribution || '' }).addTo(state.map);
}

export function toggleHeatmap(elementId, enabled) {
  const state = maps.get(elementId);
  if (!state) return;
  if (enabled) {
    if (!state.heatLayer) {
      // collect all coordinates from routes layer
      const coords = [];
      state.routesLayer.eachLayer(layer => {
        layer.toGeoJSON().features?.forEach(f => {
          if (f.geometry && f.geometry.type === 'LineString') {
            f.geometry.coordinates.forEach(c => coords.push([c[1], c[0], 0.5]));
          }
        });
      });
      state.heatLayer = L.heatLayer(coords, { radius: 25 }).addTo(state.map);
    } else {
      state.map.addLayer(state.heatLayer);
    }
  } else {
    if (state.heatLayer) state.map.removeLayer(state.heatLayer);
  }
}

export function toggleClusters(elementId, enabled) {
  const state = maps.get(elementId);
  if (!state) return;
  if (enabled) {
    if (!state.clusterLayer && typeof L.markerClusterGroup === 'function') {
      const cluster = L.markerClusterGroup();
      state.routesLayer.eachLayer(l => cluster.addLayer(l));
      state.clusterLayer = cluster;
      state.map.addLayer(cluster);
      state.map.removeLayer(state.routesLayer);
    }
  } else {
    if (state.clusterLayer) {
      state.map.removeLayer(state.clusterLayer);
      state.clusterLayer = null;
      state.map.addLayer(state.routesLayer);
    }
  }
}

export function setStickFigureForRoute(elementId, routeId, stickFigureSvg) {
  const state = maps.get(elementId);
  if (!state || !stickFigureSvg) return;

  // Remove existing stick figure marker if any
  if (state.markersLayer) {
    state.map.removeLayer(state.markersLayer);
    state.markersLayer = null;
  }

  const feature = state.routeFeatures.get(routeId);
  if (!feature) return;

  const svgIcon = L.divIcon({
    html: stickFigureSvg,
    iconSize: [32, 48],
    iconAnchor: [16, 48],
    popupAnchor: [0, -48],
    className: 'stick-figure-marker'
  });

  if (feature.geometry?.type === 'LineString' && feature.geometry.coordinates.length > 0) {
    const [lng, lat] = feature.geometry.coordinates[0];
    const marker = L.marker([lat, lng], { icon: svgIcon });
    marker.bindPopup(feature.properties?.name || 'Route');
    state.markersLayer = L.layerGroup().addTo(state.map);
    state.markersLayer.addLayer(marker);
  }
}

export function clearHighlightAndMarkers(elementId) {
  const state = maps.get(elementId);
  if (!state) return;

  if (state.highlightLayer) {
    state.map.removeLayer(state.highlightLayer);
    state.highlightLayer = null;
  }

  if (state.markersLayer) {
    state.map.removeLayer(state.markersLayer);
    state.markersLayer = null;
  }
}
