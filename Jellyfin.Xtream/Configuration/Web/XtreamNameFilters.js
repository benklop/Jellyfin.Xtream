export default function (view) {
  view.addEventListener('viewshow', async () => {
    const config = await ApiClient.getPluginConfiguration('5c534e89-6f96-4ddb-9c94-00c7b86d6709');
    const form = view.querySelector('#XtreamNameFiltersForm');
    const filtersList = view.querySelector('#NameFiltersList');

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
            <button type="button" class="move-up" is="emby-button" class="raised" style="padding: 5px 10px;">↑</button>
            <button type="button" class="move-down" is="emby-button" class="raised" style="padding: 5px 10px;">↓</button>
          </div>
          <button type="button" class="remove-filter" is="emby-button" class="raised button-delete">Remove</button>
        </div>
        <div class="inputContainer">
          <label class="inputLabel inputLabelUnfocused">Description</label>
          <input type="text" class="filter-description" is="emby-input" value="${filter.Description || ''}" placeholder="e.g., Remove UK prefix" />
        </div>
        <div class="inputContainer">
          <label class="inputLabel inputLabelUnfocused">Pattern (Regex)</label>
          <input type="text" class="filter-pattern" is="emby-input" value="${filter.Pattern || ''}" placeholder="e.g., ^UK\\s*-\\s*(.*)" required />
          <div class="fieldDescription">Regular expression pattern to match. Use capture groups to preserve parts.</div>
        </div>
        <div class="inputContainer">
          <label class="inputLabel inputLabelUnfocused">Replacement</label>
          <input type="text" class="filter-replacement" is="emby-input" value="${filter.Replacement || ''}" placeholder="e.g., $1" />
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

    function loadFilters() {
      filtersList.innerHTML = '';
      const filters = config.NameFilters || [];
      filters.forEach((filter, index) => {
        const item = createFilterItem(filter, index);
        filtersList.appendChild(item);
      });
    }

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

      // Collect all filters from the UI
      const filters = [];
      const filterItems = filtersList.querySelectorAll('.listItem');
      filterItems.forEach((item, index) => {
        filters.push({
          Pattern: item.querySelector('.filter-pattern').value,
          Replacement: item.querySelector('.filter-replacement').value,
          Description: item.querySelector('.filter-description').value,
          IsEnabled: item.querySelector('.filter-enabled').checked,
          Order: index
        });
      });

      config.NameFilters = filters;

      await ApiClient.updatePluginConfiguration('5c534e89-6f96-4ddb-9c94-00c7b86d6709', config);
      Dashboard.processPluginConfigurationUpdateResult();
    });

    loadFilters();
  });
}
