export default function (view) {
  let isInitialized = false;

  view.addEventListener('viewshow', async () => {
    const Xtream = await import(
      ApiClient.getUrl('web/ConfigurationPage', {
        name: 'Xtream.js',
      })
    ).then((module) => module.default);

    Xtream.setTabs(3);

    const pluginId = Xtream.pluginConfig.UniqueId;
    const form = view.querySelector('#XtreamNameFiltersForm');
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

      return item;
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

    // Only set up event listeners once
    if (!isInitialized) {
      view.querySelector('#AddFilter').addEventListener('click', () => {
        const newFilter = {
          Pattern: '',
          Replacement: '',
          Description: '',
          IsEnabled: true,
          Order: filtersList.children.length
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
              Order: index
            });
          });

          const currentConfig = await ApiClient.getPluginConfiguration(pluginId);
          currentConfig.NameFilters = filters;

          const result = await ApiClient.updatePluginConfiguration(pluginId, currentConfig);
          Dashboard.hideLoadingMsg();
          Dashboard.processPluginConfigurationUpdateResult(result);
        } catch (error) {
          Dashboard.hideLoadingMsg();
          console.error('Failed to save name filters:', error);
          Dashboard.alert((error && error.message) ? error.message : 'Failed to save filters. Check browser console for details.');
        }
      });

      isInitialized = true;
      
      // Load filters after setting up event listeners
      await loadFilters();
    }
  });
}
