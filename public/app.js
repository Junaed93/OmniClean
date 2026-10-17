// Storage Optimizer Client Logic
document.addEventListener('DOMContentLoaded', () => {
    // DOM Elements
    const ringFill = document.getElementById('ring-fill');
    const gaugePercent = document.getElementById('gauge-percent');
    const gaugeRatio = document.getElementById('gauge-ratio');
    const statFree = document.getElementById('stat-free');
    const statUsed = document.getElementById('stat-used');
    const statTotal = document.getElementById('stat-total');

    const categoryList = document.getElementById('category-list');
    const selectedSizeText = document.getElementById('selected-size-text');
    const btnClean = document.getElementById('btn-clean');
    const btnScan = document.getElementById('btn-scan');
    const btnSelectAll = document.getElementById('btn-select-all');
    const btnDeselectAll = document.getElementById('btn-deselect-all');
    
    const toggleSchedule = document.getElementById('toggle-schedule');
    const scheduleVal = document.getElementById('schedule-val');
    
    const consoleOutput = document.getElementById('console-output');
    const btnClearConsole = document.getElementById('btn-clear-console');
    const toast = document.getElementById('toast');

    let currentCategories = [];
    let selectedCategoryIds = new Set();
    const RING_CIRCUMFERENCE = 578; // 2 * PI * 92

    // Toast helper
    function showToast(msg) {
        toast.textContent = msg;
        toast.classList.add('show');
        setTimeout(() => toast.classList.remove('show'), 3500);
    }

    // Logger helper
    function addLog(msg, type = 'info') {
        const line = document.createElement('div');
        line.className = `log-line ${type === 'system' ? 'log-system' : type === 'success' ? 'log-success' : type === 'warn' ? 'log-warn' : ''}`;
        line.textContent = msg;
        consoleOutput.appendChild(line);
        consoleOutput.scrollTop = consoleOutput.scrollHeight;
    }

    btnClearConsole.addEventListener('click', () => {
        consoleOutput.innerHTML = '';
        addLog('[Log cleared]', 'system');
    });

    // Update Circular Gauge
    function updateGauge(percent, freeGB, totalGB) {
        gaugePercent.textContent = `${percent}%`;
        gaugeRatio.textContent = `${freeGB} GB / ${totalGB} GB`;
        
        // Offset calculation
        const offset = RING_CIRCUMFERENCE - (RING_CIRCUMFERENCE * (percent / 100));
        ringFill.style.strokeDashoffset = offset;

        // Dynamic color based on free space
        if (percent < 15) {
            ringFill.style.stroke = '#f43f5e'; // red
            ringFill.style.filter = 'drop-shadow(0 0 12px rgba(244, 63, 94, 0.5))';
        } else if (percent < 25) {
            ringFill.style.stroke = '#f59e0b'; // amber
            ringFill.style.filter = 'drop-shadow(0 0 12px rgba(245, 158, 11, 0.5))';
        } else {
            ringFill.style.stroke = '#10b981'; // emerald
            ringFill.style.filter = 'drop-shadow(0 0 12px rgba(16, 185, 129, 0.5))';
        }
    }

    // Render Categories
    function renderCategories() {
        categoryList.innerHTML = '';

        if (!currentCategories || currentCategories.length === 0) {
            categoryList.innerHTML = '<div class="loading-state">No categories detected.</div>';
            return;
        }

        currentCategories.forEach(cat => {
            const isSelected = selectedCategoryIds.has(cat.id);
            const item = document.createElement('div');
            item.className = `category-item ${isSelected ? 'selected' : ''}`;
            item.dataset.id = cat.id;

            const hasSize = cat.sizeMB > 0.1;
            const sizeDisplay = hasSize ? (cat.sizeMB > 1024 ? `${(cat.sizeMB / 1024).toFixed(2)} GB` : `${cat.sizeMB.toFixed(1)} MB`) : 'Clean';
            const badgeClass = hasSize ? 'has-data' : 'clean';

            item.innerHTML = `
                <div class="item-left">
                    <div class="custom-checkbox">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                            <polyline points="20 6 9 17 4 12"/>
                        </svg>
                    </div>
                    <div class="item-details">
                        <h4>${cat.name}</h4>
                        <p>${cat.desc}</p>
                    </div>
                </div>
                <div class="item-right">
                    <span class="size-badge ${badgeClass}">${sizeDisplay}</span>
                </div>
            `;

            item.addEventListener('click', () => {
                if (selectedCategoryIds.has(cat.id)) {
                    selectedCategoryIds.delete(cat.id);
                } else {
                    selectedCategoryIds.add(cat.id);
                }
                renderCategories();
                updateSelectedSummary();
            });

            categoryList.appendChild(item);
        });

        updateSelectedSummary();
    }

    // Update Selected Size Summary
    function updateSelectedSummary() {
        let totalMB = 0;
        currentCategories.forEach(cat => {
            if (selectedCategoryIds.has(cat.id)) {
                totalMB += (cat.sizeMB || 0);
            }
        });

        if (totalMB > 1024) {
            selectedSizeText.textContent = `${(totalMB / 1024).toFixed(2)} GB`;
        } else {
            selectedSizeText.textContent = `${totalMB.toFixed(1)} MB`;
        }

        btnClean.disabled = selectedCategoryIds.size === 0;
    }

    // Select All / Deselect All
    btnSelectAll.addEventListener('click', () => {
        selectedCategoryIds = new Set(currentCategories.map(c => c.id));
        renderCategories();
    });

    btnDeselectAll.addEventListener('click', () => {
        selectedCategoryIds.clear();
        renderCategories();
    });

    // Fetch Status from Backend
    async function fetchStatus() {
        try {
            addLog('[Scanning] Checking storage metrics and junk categories...', 'system');
            btnScan.disabled = true;

            const res = await fetch('/api/status');
            const data = await res.json();

            btnScan.disabled = false;

            if (data.disk) {
                statFree.textContent = `${data.disk.freeGB} GB`;
                statUsed.textContent = `${data.disk.usedGB} GB`;
                statTotal.textContent = `${data.disk.totalGB} GB`;
                updateGauge(data.disk.percentFree, data.disk.freeGB, data.disk.totalGB);
            }

            if (data.categories) {
                currentCategories = data.categories;
                // Auto-select items that have junk detected
                selectedCategoryIds = new Set(
                    currentCategories.filter(c => c.sizeMB > 0.5).map(c => c.id)
                );
                renderCategories();
            }

            if (data.scheduledTask) {
                toggleSchedule.checked = data.scheduledTask.enabled;
                scheduleVal.textContent = data.scheduledTask.enabled 
                    ? 'Active (Every Sunday at 12:00 PM)' 
                    : 'Disabled';
                scheduleVal.style.color = data.scheduledTask.enabled ? '#34d399' : '#94a3b8';
            }

            addLog('[Scan Complete] Storage status up to date.', 'success');
        } catch (err) {
            btnScan.disabled = false;
            addLog(`[Error] Failed to connect to optimizer service: ${err.message}`, 'warn');
        }
    }

    btnScan.addEventListener('click', fetchStatus);

    // Clean Action
    btnClean.addEventListener('click', async () => {
        if (selectedCategoryIds.size === 0) return;

        const count = selectedCategoryIds.size;
        addLog(`[Starting Cleanup] Purging ${count} selected categories...`, 'system');
        btnClean.disabled = true;
        btnClean.innerHTML = `<div class="spinner" style="width:18px;height:18px;border-width:2px;"></div> <span>Cleaning...</span>`;

        try {
            const res = await fetch('/api/clean', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ categories: Array.from(selectedCategoryIds) })
            });

            const result = await res.json();

            btnClean.innerHTML = `
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
                    <path d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M10 11v6M14 11v6"/>
                </svg>
                <span>Clean Selected Storage</span>
            `;

            if (result.logs) {
                result.logs.forEach(l => {
                    if (l.trim()) addLog(l, 'info');
                });
            }

            if (result.disk) {
                statFree.textContent = `${result.disk.freeGB} GB`;
                statUsed.textContent = `${result.disk.usedGB} GB`;
                statTotal.textContent = `${result.disk.totalGB} GB`;
                updateGauge(result.disk.percentFree, result.disk.freeGB, result.disk.totalGB);
            }

            showToast('Cleanup completed successfully!');
            addLog('[Success] Selected storage cleaned successfully.', 'success');

            // Refresh categories
            setTimeout(fetchStatus, 1000);
        } catch (err) {
            btnClean.disabled = false;
            btnClean.innerHTML = `<span>Clean Selected Storage</span>`;
            addLog(`[Error] Cleanup failed: ${err.message}`, 'warn');
            showToast('Cleanup encountered an error.');
        }
    });

    // Schedule Toggle
    toggleSchedule.addEventListener('change', async () => {
        const isEnabled = toggleSchedule.checked;
        scheduleVal.textContent = isEnabled ? 'Enabling...' : 'Disabling...';

        try {
            const res = await fetch('/api/schedule', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ enable: isEnabled })
            });

            const result = await res.json();
            if (result.success) {
                scheduleVal.textContent = isEnabled 
                    ? 'Active (Every Sunday at 12:00 PM)' 
                    : 'Disabled';
                scheduleVal.style.color = isEnabled ? '#34d399' : '#94a3b8';
                showToast(isEnabled ? 'Auto-Clean scheduled every Sunday at 12:00 PM!' : 'Auto-Clean schedule removed.');
                addLog(`[Scheduler] Auto-clean ${isEnabled ? 'activated' : 'deactivated'} in Windows Task Scheduler.`, 'success');
            }
        } catch (err) {
            toggleSchedule.checked = !isEnabled;
            scheduleVal.textContent = 'Error updating schedule';
            addLog(`[Scheduler Error] ${err.message}`, 'warn');
        }
    });

    // Initial load
    fetchStatus();
});
