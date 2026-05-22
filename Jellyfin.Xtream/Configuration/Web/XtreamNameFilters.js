export default function (view) {
  view.addEventListener('viewshow', async () => {
    const Xtream = await import(
      ApiClient.getUrl('web/ConfigurationPage', {
        name: 'Xtream.js',
      })
    ).then((module) => module.default);

    Xtream.setTabs(3);
    Xtream.renderTabLinks(view.querySelector('.tabLinks'), 'XtreamNameFilters');

    const pluginId = Xtream.pluginConfig.UniqueId;
    const filtersList = view.querySelector('#NameFiltersList');

    function escapeHtml(text) {
      const div = document.createElement('div');
      div.textContent = text;
      return div.innerHTML;
    }

    function createFilterItem(filter, index) {
      const item = document.createElement('div');
      item.className = 'listItem';
      item.style.display = 'flex';
      item.style.flexDirection = 'column';
      item.style.gap = '10px';
      item.style.padding = '10px';
      item.style.marginBottom = '10px';
      item.style.border = '1px solid rgba(255,255,255,0.1)';
      item.style.borderRadius = '4px';

      item.innerHTML = `
        <div style="display: flex; justify-content: space-between; align-items: center;">
          <div style="display: flex; gap: 10px; align-items: center;">
            <label>
              <input type="checkbox" class="filter-enabled" ${filter.IsEnabled ? 'checked' : ''} />
              Enabled
            </label>
            <button type="button" class="move-up raised" is="emby-button" style="padding: 5px 10px;">↑</button>
            <button type="button" class="move-down raised" is="emby-button" style="padding: 5px 10px;">↓</button>
          </div>
          <button type="button" class="remove-filter raised button-delete" is="emby-button">Remove</button>
        </div>
        <div class="inputContainer">
          <label class="inputLabel inputLabelUnfocused">Description</label>
          <input type="text" class="filter-description" is="emby-input" value="${escapeHtml(filter.Description || '')}" placeholder="e.g., Remove UK prefix" />
        </div>
        <div class="inputContainer">
          <label class="inputLabel inputLabelUnfocused">Pattern (Regex)</label>
          <input type="text" class="filter-pattern" is="emby-input" value="${escapeHtml(filter.Pattern || '')}" placeholder="e.g., ^UK\\s*-\\s*(.*)" />
          <div class="fieldDescription">Regular expression pattern to match. Use capture groups to preserve parts.</div>
        </div>
        <div class="inputContainer">
          <label class="inputLabel inputLabelUnfocused">Replacement</label>
          <input type="text" class="filter-replacement" is="emby-input" value="${escapeHtml(filter.Replacement || '')}" placeholder="e.g., $1" />
          <div class="fieldDescription">Replacement text. Use $1, $2, etc. to reference capture groups.</div>
        </div>
        <div class="inputContainer">
          <label class="inputLabel inputLabelUnfocused">Apply To</label>
          <div style="display: flex; gap: 15px; flex-wrap: wrap; padding: 5px 0;">
            <label style="display: flex; align-items: center; gap: 5px;">
              <input type="checkbox" class="apply-livetv-categories" ${filter.ApplyToLiveTvCategories !== false ? 'checked' : ''} />
              Live TV Categories
            </label>
            <label style="display: flex; align-items: center; gap: 5px;">
              <input type="checkbox" class="apply-livetv-items" ${filter.ApplyToLiveTvItems !== false ? 'checked' : ''} />
              Live TV Channels
            </label>
            <label style="display: flex; align-items: center; gap: 5px;">
              <input type="checkbox" class="apply-vod-categories" ${filter.ApplyToVodCategories !== false ? 'checked' : ''} />
              VOD Categories
            </label>
            <label style="display: flex; align-items: center; gap: 5px;">
              <input type="checkbox" class="apply-vod-items" ${filter.ApplyToVodItems !== false ? 'checked' : ''} />
              VOD Items
            </label>
            <label style="display: flex; align-items: center; gap: 5px;">
              <input type="checkbox" class="apply-series-categories" ${filter.ApplyToSeriesCategories !== false ? 'checked' : ''} />
              Series Categories
            </label>
            <label style="display: flex; align-items: center; gap: 5px;">
              <input type="checkbox" class="apply-series-items" ${filter.ApplyToSeriesItems !== false ? 'checked' : ''} />
              Series Items
            </label>
          </div>
        </div>
      `;

      // Handle remove button
      item.querySelector('.remove-filter').addEventListener('click', () => {
        item.remove();
      });

      // Handle move up button
      item.querySelector('.move-up').addEventListener('click', () => {
        const prev = item.previousElementSibling;
        if (prev) {
          filtersList.insertBefore(item, prev);
        }
      });

      // Handle move down button
      item.querySelector('.move-down').addEventListener('click', () => {
        const next = item.nextElementSibling;
        if (next) {
          filtersList.insertBefore(next, item);
        }
      });

      // Add input listeners for live preview
      const patternInput = item.querySelector('.filter-pattern');
      const replacementInput = item.querySelector('.filter-replacement');
      let previewTimeout;
      
      const schedulePreview = () => {
        clearTimeout(previewTimeout);
        previewTimeout = setTimeout(() => updatePreview(item), 500);
      };
      
      patternInput.addEventListener('input', schedulePreview);
      replacementInput.addEventListener('input', schedulePreview);

      return item;
    }

    async function updatePreview(filterItem) {
      const pattern = filterItem.querySelector('.filter-pattern').value;
      const replacement = filterItem.querySelector('.filter-replacement').value;
      
      if (!pattern) {
        view.querySelector('#FilterPreviewSection').style.display = 'none';
        return;
      }

      const previewSection = view.querySelector('#FilterPreviewSection');
      const previewResults = view.querySelector('#PreviewResults');
      const spinner = view.querySelector('#PreviewSpinner');
      
      previewSection.style.display = 'block';
      spinner.style.display = 'block';
      previewResults.innerHTML = '';

      try {
        const response = await ApiClient.fetch({
          type: 'POST',
          url: ApiClient.getUrl('Xtream/TestFilter'),
          dataType: 'json',
          contentType: 'application/json',
          data: JSON.stringify({
            Pattern: pattern,
            Replacement: replacement
          })
        });

        spinner.style.display = 'none';

        const renderPreviewSection = (title, items) => {
          if (!items || items.length === 0) return '';
          
          const changedItems = items.filter(i => i.Changed);
          if (changedItems.length === 0) return '';

          let html = `<h4 style="margin-top: 15px;">${escapeHtml(title)}</h4>`;
          html += '<table class="tblLibraryReport" style="width: 100%; border-collapse: collapse;">';
          html += '<thead><tr><th>Before</th><th>After</th></tr></thead><tbody>';
          
          changedItems.forEach(item => {
            html += `<tr>
              <td style="padding: 8px; border-bottom: 1px solid rgba(255,255,255,0.1);">${escapeHtml(item.Before)}</td>
              <td style="padding: 8px; border-bottom: 1px solid rgba(255,255,255,0.1); color: #00a4dc;">${escapeHtml(item.After)}</td>
            </tr>`;
          });
          
          html += '</tbody></table>';
          return html;
        };

        let html = renderPreviewSection('Live TV Categories', response.LiveTvCategories);
        html += renderPreviewSection('Live TV Channels', response.LiveTvItems);
        html += renderPreviewSection('VOD Categories', response.VodCategories);
        html += renderPreviewSection('VOD Items', response.VodItems);
        html += renderPreviewSection('Series Categories', response.SeriesCategories);
        html += renderPreviewSection('Series Items', response.SeriesItems);

        if (!html) {
          html = '<div class="fieldDescription" style="padding: 15px;">No matches found in sample data.</div>';
        }

        previewResults.innerHTML = html;
      } catch (error) {
        spinner.style.display = 'none';
        previewResults.innerHTML = `<div class="fieldDescription" style="padding: 15px; color: #ff5722;">Preview error: ${escapeHtml(error.message || 'Unknown error')}</div>`;
      }
    }

    async function loadFilters() {
      const config = await ApiClient.getPluginConfiguration(pluginId);
      filtersList.innerHTML = '';
      const filters = config.NameFilters || [];
      filters.forEach((filter, index) => {
        const item = createFilterItem(filter, index);
        filtersList.appendChild(item);
      });
    }

    await loadFilters();

    const form = view.querySelector('#XtreamNameFiltersForm');
    if (!form.dataset.listenerAttached) {
      view.querySelector('#AddFilter').addEventListener('click', () => {
        const newFilter = {
          Pattern: '',
          Replacement: '',
          Description: '',
          IsEnabled: true,
          Order: filtersList.children.length,
          ApplyToLiveTvCategories: true,
          ApplyToLiveTvItems: true,
          ApplyToVodCategories: true,
          ApplyToVodItems: true,
          ApplyToSeriesCategories: true,
          ApplyToSeriesItems: true
        };
        const item = createFilterItem(newFilter, filtersList.children.length);
        filtersList.appendChild(item);
      });

      form.addEventListener('submit', async (e) => {
        e.preventDefault();
        Dashboard.showLoadingMsg();

        try {
          // Collect all filters from the UI
          const filters = [];
          const filterItems = filtersList.querySelectorAll('.listItem');
          filterItems.forEach((item, index) => {
            const pattern = item.querySelector('.filter-pattern').value;
            
            // Validate regex pattern if not empty
            if (pattern) {
              try {
                new RegExp(pattern);
              } catch (regexError) {
                throw new Error(`Invalid regex pattern in filter ${index + 1}: ${regexError.message}`);
              }
            }

            filters.push({
              Pattern: pattern,
              Replacement: item.querySelector('.filter-replacement').value,
              Description: item.querySelector('.filter-description').value,
              IsEnabled: item.querySelector('.filter-enabled').checked,
              Order: index,
              ApplyToLiveTvCategories: item.querySelector('.apply-livetv-categories').checked,
              ApplyToLiveTvItems: item.querySelector('.apply-livetv-items').checked,
              ApplyToVodCategories: item.querySelector('.apply-vod-categories').checked,
              ApplyToVodItems: item.querySelector('.apply-vod-items').checked,
              ApplyToSeriesCategories: item.querySelector('.apply-series-categories').checked,
              ApplyToSeriesItems: item.querySelector('.apply-series-items').checked
            });
          });

          const currentConfig = await ApiClient.getPluginConfiguration(pluginId);
          currentConfig.NameFilters = filters;

          const result = await ApiClient.updatePluginConfiguration(pluginId, currentConfig);
          Xtream.logConfigurationChange('Name Filters');
          Dashboard.hideLoadingMsg();
          Dashboard.processPluginConfigurationUpdateResult(result);
        } catch (error) {
          Dashboard.hideLoadingMsg();
          console.error('Failed to save name filters:', error);
          Dashboard.alert((error && error.message) ? error.message : 'Failed to save filters. Check browser console for details.');
        }
      });

      form.dataset.listenerAttached = 'true';
    }
  });
}
