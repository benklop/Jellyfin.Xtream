export default function (view) {
  let isPopulated = false;
  let currentData = null;

  const populate = (Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    const getConfig = ApiClient.getPluginConfiguration(pluginId);
    const visible = view.querySelector("#Visible");
    const tmdbOverride = view.querySelector("#TmdbOverride");
    const collapseCategories = view.querySelector("#CollapseCategories");
    getConfig.then((config) => {
      visible.checked = config.IsSeriesVisible;
      tmdbOverride.checked = config.IsTmdbSeriesOverride;
      collapseCategories.checked = config.CollapseSeriesCategories;
    });
    const table = view.querySelector('#SeriesContent');
    return Xtream.populateCategoriesTable(
      table,
      () => getConfig.then((config) => config.Series),
      () => Xtream.fetchJson('Xtream/SeriesCategories'),
      (categoryId) => Xtream.fetchJson(`Xtream/SeriesCategories/${categoryId}`),
    ).then((data) => {
      currentData = data;
      isPopulated = true;
      return data;
    });
  };

  view.addEventListener("viewshow", () => import(
    ApiClient.getUrl("web/ConfigurationPage", {
      name: "Xtream.js",
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    Xtream.setTabs(5);

    // Only populate if not already populated
    const populatePromise = isPopulated ? Promise.resolve(currentData) : populate(Xtream);

    populatePromise.then((data) => {
      // Set up refresh button
      const refreshBtn = view.querySelector('#RefreshCategories');
      refreshBtn.onclick = () => {
        populate(Xtream).then((newData) => {
          currentData = newData;
        });
      };

      // Set up form submit (only once)
      const form = view.querySelector('#XtreamSeriesForm');
      if (!form.dataset.listenerAttached) {
        form.addEventListener('submit', (e) => {
          Dashboard.showLoadingMsg();
          const pluginId = Xtream.pluginConfig.UniqueId;
          const visible = view.querySelector("#Visible");
          const tmdbOverride = view.querySelector("#TmdbOverride");
          const collapseCategories = view.querySelector("#CollapseCategories");

          ApiClient.getPluginConfiguration(pluginId).then((config) => {
            config.IsSeriesVisible = visible.checked;
            config.IsTmdbSeriesOverride = tmdbOverride.checked;
            config.CollapseSeriesCategories = collapseCategories.checked;
            config.Series = currentData;
            ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
              Dashboard.processPluginConfigurationUpdateResult(result);
            });
          });

          e.preventDefault();
          return false;
        });
        form.dataset.listenerAttached = 'true';
      }
    }).catch((error) => {
      console.error('Failed to load series categories:', error);
      Dashboard.hideLoadingMsg();
      const table = view.querySelector('#SeriesContent');
      table.innerHTML = '';
      const errorRow = document.createElement('tr');
      const errorCell = document.createElement('td');
      errorCell.colSpan = 3;
      errorCell.style.color = '#ff6b6b';
      errorCell.style.padding = '16px';
      errorCell.innerHTML = 'Failed to load categories. Please check:<br>' +
        '1. Xtream credentials are configured (Credentials tab)<br>' +
        '2. Xtream server is accessible<br>' +
        '3. Browser console for detailed errors';
      errorRow.appendChild(errorCell);
      table.appendChild(errorRow);
    });
  }));
}
