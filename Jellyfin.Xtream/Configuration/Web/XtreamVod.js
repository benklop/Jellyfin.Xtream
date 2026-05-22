export default function (view) {
  let isPopulated = false;
  let currentData = null;

  const populate = (Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    const getConfig = ApiClient.getPluginConfiguration(pluginId);
    const visible = view.querySelector("#Visible");
    getConfig.then((config) => visible.checked = config.IsVodVisible);
    const tmdbOverride = view.querySelector("#TmdbOverride");
    getConfig.then((config) => tmdbOverride.checked = config.IsTmdbVodOverride);
    const collapseCategories = view.querySelector("#CollapseCategories");
    getConfig.then((config) => collapseCategories.checked = config.CollapseVodCategories);
    const table = view.querySelector('#VodContent');
    return Xtream.populateCategoriesTable(
      table,
      () => getConfig.then((config) => config.Vod),
      () => Xtream.fetchJson('Xtream/VodCategories'),
      (categoryId) => Xtream.fetchJson(`Xtream/VodCategories/${categoryId}`),
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
    Xtream.setTabs(4);
    Xtream.renderTabLinks(view.querySelector('.tabLinks'), 'XtreamVod');

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
      const form = view.querySelector('#XtreamVodForm');
      if (!form.dataset.listenerAttached) {
        form.addEventListener('submit', (e) => {
          Dashboard.showLoadingMsg();
          const pluginId = Xtream.pluginConfig.UniqueId;
          const visible = view.querySelector("#Visible");
          const tmdbOverride = view.querySelector("#TmdbOverride");
          const collapseCategories = view.querySelector("#CollapseCategories");

          ApiClient.getPluginConfiguration(pluginId).then((config) => {
            config.IsVodVisible = visible.checked;
            config.IsTmdbVodOverride = tmdbOverride.checked;
            config.CollapseVodCategories = collapseCategories.checked;
            config.Vod = currentData;
            ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
              Xtream.logConfigurationChange('VOD');
              Dashboard.processPluginConfigurationUpdateResult(result);
            });
          });

          e.preventDefault();
          return false;
        });
        form.dataset.listenerAttached = 'true';
      }
    }).catch((error) => {
      console.error('Failed to load VOD categories:', error);
      Dashboard.hideLoadingMsg();
      const table = view.querySelector('#VodContent');
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
