export default function (view) {
  let isPopulated = false;
  let currentData = null;

  const populate = (Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    const getConfig = ApiClient.getPluginConfiguration(pluginId);
    const visible = view.querySelector("#Visible");
    getConfig.then((config) => visible.checked = config.IsCatchupVisible);
    const table = view.querySelector('#LiveContent');
    return Xtream.populateCategoriesTable(
      table,
      () => getConfig.then((config) => config.LiveTv),
      () => Xtream.fetchJson('Xtream/LiveCategories'),
      (categoryId) => Xtream.fetchJson(`Xtream/LiveCategories/${categoryId}`),
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
    Xtream.setTabs(1);

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
      const form = view.querySelector('#XtreamLiveForm');
      if (!form.dataset.listenerAttached) {
        form.addEventListener('submit', (e) => {
          Dashboard.showLoadingMsg();
          const pluginId = Xtream.pluginConfig.UniqueId;
          const visible = view.querySelector("#Visible");

          ApiClient.getPluginConfiguration(pluginId).then((config) => {
            config.IsCatchupVisible = visible.checked;
            config.LiveTv = currentData;
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
