export default function (view) {
  view.addEventListener('viewshow', () => import(
    ApiClient.getUrl('web/ConfigurationPage', {
      name: 'Xtream.js',
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    Xtream.setTabs(6);
    Xtream.renderTabLinks(view.querySelector('.tabLinks'), 'XtreamXmlTv');

    ApiClient.getPluginConfiguration(pluginId)
      .then((config) => {
        view.querySelector('#UseXmlTv').checked = config.UseXmlTv;
        view.querySelector('#XmlTvUrl').value = config.XmlTvUrl || '';
        view.querySelector('#XmlTvHistoricalDays').value = config.XmlTvHistoricalDays || 0;
        view.querySelector('#XmlTvCacheMinutes').value = config.XmlTvCacheMinutes || 10;
        view.querySelector('#XmlTvSupportsTimeshift').checked = config.XmlTvSupportsTimeshift;
        view.querySelector('#XmlTvDiskCache').checked = config.XmlTvDiskCache;
        view.querySelector('#XmlTvCachePath').value = config.XmlTvCachePath || '';
      })
      .catch((err) => {
        console.error('Error loading configuration:', err);
        Dashboard.alert({
          message: 'Failed to load plugin configuration. Please check the server logs.'
        });
      });

    view.querySelector('#XtreamXmlTvForm').addEventListener('submit', (e) => {
      e.preventDefault();

      const useXmlTv = view.querySelector('#UseXmlTv').checked;
      const xmlTvUrl = view.querySelector('#XmlTvUrl').value.trim();
      const historicalDays = Math.max(0, parseInt(view.querySelector('#XmlTvHistoricalDays').value || '0', 10));
      const cacheMinutes = Math.max(1, parseInt(view.querySelector('#XmlTvCacheMinutes').value || '10', 10));
      const diskCache = view.querySelector('#XmlTvDiskCache').checked;
      const cachePath = view.querySelector('#XmlTvCachePath').value.trim();

      if (useXmlTv && xmlTvUrl && !xmlTvUrl.match(/^https?:\/\//i)) {
        Dashboard.alert({
          message: 'XMLTV URL must be an absolute http(s) URL, or a relative path (e.g. xmltv.php?...)'
        });
        return false;
      }

      ApiClient.getPluginConfiguration(pluginId)
        .then((config) => {
          config.UseXmlTv = useXmlTv;
          config.XmlTvUrl = xmlTvUrl;
          config.XmlTvHistoricalDays = historicalDays;
          config.XmlTvCacheMinutes = cacheMinutes;
          config.XmlTvSupportsTimeshift = view.querySelector('#XmlTvSupportsTimeshift').checked;
          config.XmlTvDiskCache = diskCache;
          config.XmlTvCachePath = cachePath;

          return ApiClient.updatePluginConfiguration(pluginId, config);
        })
        .then((result) => {
          Xtream.logConfigurationChange('XMLTV');
          Dashboard.processPluginConfigurationUpdateResult(result);
        })
        .catch((err) => {
          console.error('Error saving configuration:', err);
          Dashboard.alert({
            message: 'Failed to save configuration. Please check the browser console and server logs.'
          });
        });

      return false;
    });
  }));
}
