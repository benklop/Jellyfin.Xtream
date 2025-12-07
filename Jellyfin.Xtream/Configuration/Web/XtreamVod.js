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

          ApiClient.getPluginConfiguration(pluginId).then((config) => {
            config.IsVodVisible = visible.checked;
            config.IsTmdbVodOverride = tmdbOverride.checked;
            config.Vod = currentData;
            ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
              Dashboard.processPluginConfigurationUpdateResult(result);
            });
          });

          e.preventDefault();
          return false;
        });
        form.dataset.listenerAttached = 'true';
      }
    });
  }));
}
