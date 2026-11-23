// ParkingOCR Frontend Application
(function () {
  'use strict';

  // Application State
  const state = {
    isHealthy: null,
    samples: [],
    selectedSampleId: null,
    selectedFile: null,
    selectedPreview: null,
    isLoading: false,
    result: null,
    activeImageTab: 'annotated', // 'annotated', 'crop', 'original'
    filterCountryCode: true,
    confidenceThreshold: 0.35,
    showSettings: false,
    history: []
  };

  // DOM Elements
  const el = {
    healthIndicator: document.getElementById('health-indicator'),
    healthText: document.getElementById('health-text'),
    sampleGrid: document.getElementById('sample-grid'),
    dropzoneBox: document.getElementById('dropzone-box'),
    fileInput: document.getElementById('file-input'),
    previewContainer: document.getElementById('preview-container'),
    previewImage: document.getElementById('preview-image'),
    loadingOverlay: document.getElementById('loading-overlay'),
    changeImageBtn: document.getElementById('change-image-btn'),
    settingsToggleBtn: document.getElementById('settings-toggle-btn'),
    settingsPanel: document.getElementById('settings-panel'),
    filterCountryToggle: document.getElementById('filter-country-toggle'),
    confThresholdSlider: document.getElementById('conf-threshold-slider'),
    confThresholdValue: document.getElementById('conf-threshold-value'),
    rescanBtn: document.getElementById('rescan-btn'),
    resultsSection: document.getElementById('results-section'),
    historySection: document.getElementById('history-section'),
    historyBar: document.getElementById('history-bar'),
  };

  // Initialize
  async function init() {
    setupEventListeners();
    await checkHealth();
    await loadSamples();
  }

  // Health Check
  async function checkHealth() {
    try {
      const res = await fetch('/api/health');
      if (res.ok) {
        state.isHealthy = true;
        el.healthIndicator.className = 'status-dot';
        el.healthText.textContent = 'Engine Ready';
      } else {
        throw new Error();
      }
    } catch {
      state.isHealthy = false;
      el.healthIndicator.className = 'status-dot';
      el.healthIndicator.style.backgroundColor = 'var(--accent-rose)';
      el.healthIndicator.style.boxShadow = '0 0 10px rgba(244, 63, 94, 0.7)';
      el.healthText.textContent = 'Offline';
    }
  }

  // Load Samples
  async function loadSamples() {
    try {
      const res = await fetch('/api/ocr/samples');
      if (res.ok) {
        state.samples = await res.json();
        renderSamples();
      }
    } catch (err) {
      console.error('Failed to load samples:', err);
    }
  }

  function renderSamples() {
    if (!el.sampleGrid) return;
    el.sampleGrid.innerHTML = '';

    state.samples.forEach(sample => {
      const card = document.createElement('div');
      card.className = `sample-card ${state.selectedSampleId === sample.id ? 'active' : ''}`;
      
      let badgeHtml = '';
      if (sample.countryCode === 'CH') {
        badgeHtml = '<span class="sample-badge-ro" style="background:#dc2626; color:#fff;">CH</span>';
      } else if (sample.countryCode === 'UK') {
        badgeHtml = '<span class="sample-badge-ro" style="background:#facc15; color:#0f172a; font-weight:900;">UK</span>';
      } else if (sample.hasEuroband && sample.countryCode) {
        badgeHtml = `<span class="sample-badge-ro">${sample.countryCode}</span>`;
      } else if (!sample.hasEuroband) {
        badgeHtml = '<span class="sample-badge-ro" style="background:#475569; color:#e2e8f0;">No Strip</span>';
      }

      card.innerHTML = `
        <div class="sample-img-box">
          <img src="${sample.thumbnailBase64 || ''}" alt="${sample.title}" />
          ${badgeHtml}
        </div>
        <div class="sample-plate-text font-plate">${sample.expectedPlate}</div>
        <div class="sample-meta">${sample.title}</div>
      `;
      card.addEventListener('click', () => selectSample(sample));
      el.sampleGrid.appendChild(card);
    });
  }

  // Sample Selection & Recognition
  async function selectSample(sample) {
    if (state.isLoading) return;
    state.selectedSampleId = sample.id;
    state.selectedFile = null;
    state.selectedPreview = sample.thumbnailBase64;
    renderSamples();
    showPreview(sample.thumbnailBase64);

    state.isLoading = true;
    updateLoadingState(true);

    try {
      const res = await fetch(
        `/api/ocr/sample/${encodeURIComponent(sample.id)}?filterCountryCode=${state.filterCountryCode}&confidenceThreshold=${state.confidenceThreshold}`,
        { method: 'POST' }
      );
      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        renderResult({ success: false, message: errData.message || `Sample recognition failed (${res.status})` });
        return;
      }
      const data = await res.json();
      state.result = data;
      renderResult(data);
      addToHistory(data, sample.thumbnailBase64);
    } catch (err) {
      console.error('Sample recognition error:', err);
      renderResult({ success: false, message: `Sample error: ${err.message}` });
    } finally {
      state.isLoading = false;
      updateLoadingState(false);
    }
  }

  // User File Upload & Recognition
  async function processFile(file) {
    if (!file || !file.type.startsWith('image/')) {
      alert('Please select a valid image file (JPEG, PNG, WebP).');
      return;
    }

    state.selectedSampleId = null;
    renderSamples();
    state.selectedFile = file;

    const reader = new FileReader();
    reader.onload = async () => {
      const base64 = reader.result;
      state.selectedPreview = base64;
      showPreview(base64);

      state.isLoading = true;
      updateLoadingState(true);

      try {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('filterCountryCode', String(state.filterCountryCode));
        formData.append('confidenceThreshold', String(state.confidenceThreshold));
        formData.append('drawDebugAnnotations', 'true');

        const res = await fetch('/api/ocr/recognize', {
          method: 'POST',
          body: formData
        });

        if (!res.ok) {
          const errData = await res.json().catch(() => ({}));
          const errMsg = errData.message || errData.title || `Server error (${res.status})`;
          renderResult({ success: false, message: errMsg });
          return;
        }

        const data = await res.json();
        state.result = data;
        renderResult(data);
        addToHistory(data, base64);
      } catch (err) {
        console.error('File recognition error:', err);
        renderResult({ success: false, message: `Upload error: ${err.message}` });
      } finally {
        state.isLoading = false;
        updateLoadingState(false);
      }
    };
    reader.readAsDataURL(file);
  }

  function showPreview(src) {
    if (!src) {
      el.dropzoneBox.style.display = 'flex';
      el.previewContainer.style.display = 'none';
      el.rescanBtn.style.display = 'none';
      return;
    }
    el.previewImage.src = src;
    el.dropzoneBox.style.display = 'none';
    el.previewContainer.style.display = 'block';
    el.rescanBtn.style.display = 'inline-flex';
  }

  function updateLoadingState(loading) {
    if (loading) {
      el.loadingOverlay.style.display = 'flex';
    } else {
      el.loadingOverlay.style.display = 'none';
    }
  }

  // Render European License Plate Badge (Euroband, Swiss, UK, or Clean)
  function renderPlateBadgeHtml(plateText, countryCode, plateType) {
    const isTemp = (plateType || '').toLowerCase().includes('temporary');
    const isSwiss = countryCode === 'CH';
    const isUk = countryCode === 'UK' || countryCode === 'GB';
    const hasEuroband = Boolean(countryCode && countryCode !== 'CH' && countryCode !== 'NONE');
    
    let containerClass = 'plate-badge-container';
    if (isTemp) containerClass += ' plate-badge-temp';
    if (isUk) containerClass += ' plate-badge-uk-yellow';

    return `
      <div class="${containerClass}">
        ${isSwiss ? `
          <div class="plate-badge-swiss-shield" title="Swiss Confederation Coat of Arms">
            <div class="plate-badge-swiss-cross"></div>
          </div>
        ` : (hasEuroband && !isTemp ? `
          <div class="plate-badge-euroband">
            <svg class="plate-badge-stars" viewBox="0 0 100 100">
              <circle cx="50" cy="50" r="42" fill="none" />
              ${[...Array(12)].map((_, i) => {
                const angle = (i * 30 * Math.PI) / 180;
                const cx = 50 + 35 * Math.sin(angle);
                const cy = 50 - 35 * Math.cos(angle);
                return `<polygon points="${cx},${cy-5} ${cx+3.5},${cy+4} ${cx-5},${cy-1.5} ${cx+5},${cy-1.5} ${cx-3.5},${cy+4}" fill="#facc15" />`;
              }).join('')}
            </svg>
            <span class="plate-badge-ro">${countryCode}</span>
          </div>
        ` : '')}
        <div class="plate-badge-text font-plate">${plateText || 'ZH 84521'}</div>
      </div>
    `;
  }

  // Render Recognition Results
  function renderResult(data) {
    if (!el.resultsSection) return;
    el.resultsSection.style.display = 'block';

    if (!data.success) {
      el.resultsSection.innerHTML = `
        <div class="card" style="border-color: rgba(244, 63, 94, 0.4); background: rgba(244, 63, 94, 0.1); text-align: center; padding: 2rem;">
          <h3 style="color: #fca5a5; font-size: 1.1rem; font-weight: 700;">No License Plate Recognized</h3>
          <p style="color: #cbd5e1; font-size: 0.8rem; margin-top: 0.5rem;">${data.message || 'Could not locate a clear license plate. Please test another image or adjust threshold.'}</p>
        </div>
      `;
      return;
    }

    const countryCode = data.countryCode || data.validation?.countryCode || null;
    const countryName = data.validation?.countryName || (countryCode === 'CH' ? 'Switzerland' : (countryCode ? 'European' : 'Standard'));
    const isSwiss = countryCode === 'CH';
    const hasEuroband = Boolean(countryCode && !isSwiss && countryCode !== 'NONE');
    const confPct = Math.round(data.confidence * 100);

    el.resultsSection.innerHTML = `
      <div class="results-card">
        <div class="results-header">
          <div>
            <div style="font-size: 0.72rem; color: #94a3b8; font-weight: 600; text-transform: uppercase; margin-bottom: 0.5rem;">
              Recognized License Plate
            </div>
            ${renderPlateBadgeHtml(data.plateText, countryCode, '')}
          </div>

          <div class="metrics-grid">
            <div class="metric-card">
              <span class="metric-label">Country</span>
              <span class="metric-value" style="color: #60a5fa; font-size: 1.1rem;">${countryName}</span>
            </div>
            <div class="metric-card">
              <span class="metric-label">Strip / Emblem</span>
              <span class="metric-value font-mono" style="font-size: 1.05rem; color: ${hasEuroband ? '#38bdf8' : (isSwiss ? '#f87171' : '#94a3b8')};">
                ${hasEuroband ? `Euroband (${countryCode})` : (isSwiss ? 'Swiss Shield' : 'No Strip')}
              </span>
            </div>
            <div class="metric-card">
              <span class="metric-label">Confidence</span>
              <span class="metric-value font-mono" style="color: #34d399;">${data.confidencePercentage}</span>
            </div>
            <div class="metric-card">
              <span class="metric-label">Latency</span>
              <span class="metric-value font-mono">${data.processingTimeMs} ms</span>
            </div>
          </div>
        </div>

        <!-- Filter Strip Highlight Banner -->
        <div class="filter-banner">
          <div style="display: flex; align-items: center; gap: 0.75rem;">
            <div style="width: 2.2rem; height: 2.2rem; border-radius: 0.5rem; background: ${isSwiss ? 'rgba(220, 38, 38, 0.2)' : 'rgba(59, 130, 246, 0.15)'}; border: 1px solid ${isSwiss ? 'rgba(220, 38, 38, 0.4)' : 'rgba(59, 130, 246, 0.3)'}; display: flex; align-items: center; justify-content: center; color: ${isSwiss ? '#f87171' : '#60a5fa'}; font-weight: 800;">
              ${countryCode || 'EU'}
            </div>
            <div>
              <div style="font-size: 0.8rem; font-weight: 700; color: #f8fafc;">
                European Identification & Emblem Filter:
                <span class="filter-badge-pill ${hasEuroband || isSwiss ? 'active' : ''}">
                  ${hasEuroband ? `Euroband '${countryCode}' Removed` : (isSwiss ? 'Swiss National Shield Isolated' : 'Non-Euroband High-Contrast Plate')}
                </span>
              </div>
              <div style="font-size: 0.72rem; color: #94a3b8;">
                ${hasEuroband
                  ? `Blue European country strip ('${countryCode}') detected and cleanly stripped from registration letters.`
                  : (isSwiss
                    ? 'Swiss cross shield & canton coat of arms isolated. Clean Swiss registration number extracted.'
                    : 'Plate detected and extracted without Euroband prefix.')}
              </div>
            </div>
          </div>

          <div class="filter-raw-box">
            <span style="color: #64748b;">Raw OCR:</span>
            <span style="color: #f87171; font-weight: 800;">${data.rawText}</span>
            <span style="color: #64748b;">➔</span>
            <span style="color: #64748b;">Clean Plate:</span>
            <span style="color: #34d399; font-weight: 800;">${data.plateText}</span>
          </div>
        </div>

        <!-- Computer Vision & Glyphs Grid -->
        <div class="detail-grid">
          <!-- Computer Vision Panel -->
          <div class="card" style="padding: 1rem;">
            <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.75rem;">
              <span style="font-size: 0.75rem; font-weight: 700; text-transform: uppercase; color: #cbd5e1;">
                Visual Inspection
              </span>
              <div class="tab-group" id="cv-tab-group">
                <button class="tab-btn ${state.activeImageTab === 'annotated' ? 'active' : ''}" data-tab="annotated">Annotated</button>
                ${data.plateCropBase64 ? `<button class="tab-btn ${state.activeImageTab === 'crop' ? 'active' : ''}" data-tab="crop">Plate Crop</button>` : ''}
                <button class="tab-btn ${state.activeImageTab === 'original' ? 'active' : ''}" data-tab="original">Original</button>
              </div>
            </div>

            <div style="position: relative; aspect-ratio: 16/9; background: #020617; border-radius: 0.75rem; overflow: hidden; display: flex; align-items: center; justify-content: center; border: 1px solid var(--border-color);">
              <img id="cv-active-image" style="max-width: 100%; max-height: 100%; object-fit: contain;" src="${getActiveImageSrc(data)}" />
            </div>

            ${data.boundingBox ? `
              <div style="display: flex; justify-content: space-between; font-size: 0.7rem; color: #94a3b8; margin-top: 0.5rem; padding: 0 0.25rem;">
                <span>Bounding Box: [X: ${data.boundingBox.x}, Y: ${data.boundingBox.y}, W: ${data.boundingBox.width}, H: ${data.boundingBox.height}]</span>
                <span>YOLOv11-nano Deep Learning</span>
              </div>
            ` : ''}
          </div>

          <!-- Character Segmentation Panel -->
          <div class="card" style="padding: 1rem; display: flex; flex-direction: column; justify-content: space-between;">
            <div>
              <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem;">
                <span style="font-size: 0.75rem; font-weight: 700; text-transform: uppercase; color: #cbd5e1;">
                  Character Glyphs (${data.characters.length})
                </span>
                <span style="font-size: 0.7rem; color: #94a3b8; font-family: monospace;">DIN 1451 Characters</span>
              </div>

              <div class="glyph-grid">
                ${data.characters.map((c, i) => `
                  <div class="glyph-card ${c.isCountryCode ? 'country-code' : ''}">
                    <span style="font-size: 0.65rem; color: #94a3b8;">${c.isCountryCode ? 'Strip' : '#' + (i + 1)}</span>
                    <div class="glyph-char font-plate">${c.character}</div>
                    <span style="font-size: 0.65rem; font-family: monospace; color: #34d399;">${Math.round(c.confidence * 100)}%</span>
                  </div>
                `).join('')}
              </div>

              <!-- Extracted Plate Text Stream -->
              <div style="margin-top: 1rem; background: rgba(2, 6, 23, 0.7); border: 1px solid var(--border-color); border-radius: 0.65rem; padding: 0.75rem;">
                <span style="font-size: 0.7rem; font-weight: 700; color: #94a3b8; display: block; margin-bottom: 0.5rem;">
                  Clean Plate Text Output
                </span>
                <div style="background: rgba(15, 23, 42, 0.8); padding: 0.65rem; border-radius: 0.4rem; text-align: center;">
                  <span class="font-plate" style="font-size: 1.4rem; font-weight: 900; color: #34d399; letter-spacing: 0.15em;">${data.plateText || '-'}</span>
                </div>
              </div>
            </div>

            <!-- Confidence Bar -->
            <div style="margin-top: 1rem; padding-top: 0.75rem; border-top: 1px solid var(--border-color);">
              <div style="display: flex; justify-content: space-between; font-size: 0.72rem; margin-bottom: 0.35rem;">
                <span style="color: #94a3b8;">Average Character Confidence</span>
                <span style="font-weight: 800; font-family: monospace; color: #34d399;">${data.confidencePercentage}</span>
              </div>
              <div style="width: 100%; height: 6px; background: #0f172a; border-radius: 9999px; overflow: hidden;">
                <div style="width: ${confPct}%; height: 100%; background: #10b981; border-radius: 9999px; transition: width 0.5s ease;"></div>
              </div>
            </div>
          </div>
        </div>
      </div>
    `;

    // Setup image tab buttons
    const tabBtns = el.resultsSection.querySelectorAll('#cv-tab-group .tab-btn');
    tabBtns.forEach(btn => {
      btn.addEventListener('click', (e) => {
        const tab = e.target.getAttribute('data-tab');
        state.activeImageTab = tab;
        tabBtns.forEach(b => b.classList.remove('active'));
        e.target.classList.add('active');
        const img = document.getElementById('cv-active-image');
        if (img) img.src = getActiveImageSrc(data);
      });
    });
  }

  function getActiveImageSrc(data) {
    if (state.activeImageTab === 'annotated' && data.processedImageBase64) {
      return data.processedImageBase64;
    }
    if (state.activeImageTab === 'crop' && data.plateCropBase64) {
      return data.plateCropBase64;
    }
    return state.selectedPreview || '';
  }

  // History Tracking
  function addToHistory(data, thumbnail) {
    const item = {
      id: Math.random().toString(36).substring(2, 9),
      time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
      plateText: data.plateText || 'Unrecognized',
      confidence: data.confidencePercentage,
      durationMs: data.processingTimeMs,
      thumbnail: data.plateCropBase64 || thumbnail || '',
      fullResult: data,
      previewSrc: thumbnail
    };

    state.history.unshift(item);
    if (state.history.length > 8) state.history.pop();
    renderHistory();
  }

  function renderHistory() {
    if (!el.historySection || state.history.length === 0) return;
    el.historySection.style.display = 'block';
    el.historyBar.innerHTML = '';

    state.history.forEach(item => {
      const card = document.createElement('div');
      card.className = 'history-item';
      card.innerHTML = `
        <div class="history-thumb">
          <img src="${item.thumbnail}" alt="${item.plateText}" />
        </div>
        <div>
          <div style="font-weight: 800; font-family: monospace; font-size: 0.8rem; color: #f8fafc;">
            ${item.plateText}
          </div>
          <div style="font-size: 0.68rem; color: #94a3b8;">
            ${item.time} • <span style="color: #34d399;">${item.confidence}</span> • ${item.durationMs}ms
          </div>
        </div>
      `;
      card.addEventListener('click', () => {
        state.result = item.fullResult;
        state.selectedPreview = item.previewSrc;
        showPreview(item.previewSrc);
        renderResult(item.fullResult);
      });
      el.historyBar.appendChild(card);
    });
  }

  // Setup Event Listeners
  function setupEventListeners() {
    // Dropzone Click
    el.dropzoneBox.addEventListener('click', () => el.fileInput.click());

    // File Input Change
    el.fileInput.addEventListener('change', (e) => {
      if (e.target.files && e.target.files[0]) {
        processFile(e.target.files[0]);
      }
    });

    // Drag and Drop
    el.dropzoneBox.addEventListener('dragover', (e) => {
      e.preventDefault();
      el.dropzoneBox.classList.add('drag-over');
    });

    el.dropzoneBox.addEventListener('dragleave', (e) => {
      e.preventDefault();
      el.dropzoneBox.classList.remove('drag-over');
    });

    el.dropzoneBox.addEventListener('drop', (e) => {
      e.preventDefault();
      el.dropzoneBox.classList.remove('drag-over');
      if (e.dataTransfer.files && e.dataTransfer.files[0]) {
        processFile(e.dataTransfer.files[0]);
      }
    });

    // Clipboard Paste (Ctrl+V)
    window.addEventListener('paste', (e) => {
      const items = e.clipboardData?.items;
      if (!items) return;
      for (let i = 0; i < items.length; i++) {
        if (items[i].type.indexOf('image') !== -1) {
          const blob = items[i].getAsFile();
          if (blob) processFile(blob);
          break;
        }
      }
    });

    // Change Image
    el.changeImageBtn.addEventListener('click', () => {
      state.selectedPreview = null;
      state.selectedFile = null;
      state.selectedSampleId = null;
      el.fileInput.value = '';
      showPreview(null);
      renderSamples();
    });

    // Settings Toggle
    el.settingsToggleBtn.addEventListener('click', () => {
      state.showSettings = !state.showSettings;
      el.settingsPanel.style.display = state.showSettings ? 'grid' : 'none';
    });

    // Filter Country Toggle
    el.filterCountryToggle.addEventListener('change', (e) => {
      state.filterCountryCode = e.target.checked;
    });

    // Confidence Threshold Slider
    el.confThresholdSlider.addEventListener('input', (e) => {
      const val = parseFloat(e.target.value);
      state.confidenceThreshold = val;
      el.confThresholdValue.textContent = Math.round(val * 100) + '%';
    });

    // Re-scan Button
    el.rescanBtn.addEventListener('click', () => {
      if (state.selectedSampleId) {
        const sample = state.samples.find(s => s.id === state.selectedSampleId);
        if (sample) selectSample(sample);
      } else if (state.selectedFile) {
        processFile(state.selectedFile);
      }
    });
  }

  // Start app on DOMContentLoaded
  document.addEventListener('DOMContentLoaded', init);
})();
