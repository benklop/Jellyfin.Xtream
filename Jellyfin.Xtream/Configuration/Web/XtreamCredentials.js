export default function (view) {
  let currentConfig = null;

  view.addEventListener("viewshow", () => import(
    window.ApiClient.getUrl("web/ConfigurationPage", {
      name: "Xtream.js",
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    Xtream.setTabs(0);

    const renderAdditionalCredentials = (credentials) => {
      const container = view.querySelector('#AdditionalCredentialsList');
      container.innerHTML = '';

      if (!credentials || credentials.length === 0) {
        container.innerHTML = '<div class="fieldDescription" style="padding: 1em;">No additional credentials configured.</div>';
        return;
      }

      credentials.forEach((cred, index) => {
        const credDiv = document.createElement('div');
        credDiv.className = 'listItem';
        credDiv.style.padding = '1em';
        credDiv.style.marginBottom = '0.5em';
        credDiv.style.border = '1px solid rgba(255,255,255,0.1)';
        credDiv.style.borderRadius = '4px';

        credDiv.innerHTML = `
          <div style="display: flex; align-items: center; gap: 1em;">
            <input type="checkbox" class="cred-enabled" data-index="${index}" ${cred.IsEnabled ? 'checked' : ''} 
                   style="width: auto; margin: 0;" />
            <div style="flex: 1;">
              <input type="text" class="cred-label" data-index="${index}" 
                     placeholder="Label (optional)" value="${cred.Label || ''}"
                     is="emby-input" style="margin-bottom: 0.5em;" />
              <div style="display: flex; gap: 0.5em;">
                <input type="text" class="cred-username" data-index="${index}" 
                       placeholder="Username" value="${cred.Username}"
                       is="emby-input" style="flex: 1;" />
                <input type="password" class="cred-password" data-index="${index}" 
                       placeholder="Password" value="${cred.Password}"
                       is="emby-input" style="flex: 1;" />
              </div>
            </div>
            <button type="button" class="cred-delete" data-index="${index}" 
                    is="emby-button" class="button-flat">
              <i class="md-icon">delete</i>
            </button>
          </div>
        `;

        container.appendChild(credDiv);
      });

      // Add event listeners for delete buttons
      container.querySelectorAll('.cred-delete').forEach(btn => {
        btn.addEventListener('click', (e) => {
          const index = parseInt(e.currentTarget.getAttribute('data-index'));
          currentConfig.Credentials.splice(index, 1);
          renderAdditionalCredentials(currentConfig.Credentials);
        });
      });
    };

    const loadConfig = async () => {
      Dashboard.showLoadingMsg();
      const config = await ApiClient.getPluginConfiguration(pluginId);
      currentConfig = config;
      view.querySelector('#BaseUrl').value = config.BaseUrl;
      view.querySelector('#Username').value = config.Username;
      view.querySelector('#Password').value = config.Password;
      view.querySelector('#UserAgent').value = config.UserAgent;
      view.querySelector('#VodVisible').checked = config.IsVodVisible;
      view.querySelector('#SeriesVisible').checked = config.IsSeriesVisible;

      // Initialize Credentials array if it doesn't exist
      if (!config.Credentials) {
        config.Credentials = [];
      }
      
      renderAdditionalCredentials(config.Credentials);
      Dashboard.hideLoadingMsg();
    };

    const reloadStatus = () => {
      const status = view.querySelector("#ProviderStatus");
      const expiry = view.querySelector("#ProviderExpiry");
      const cons = view.querySelector("#ProviderConnections");
      const maxCons = view.querySelector("#ProviderMaxConnections");
      const time = view.querySelector("#ProviderTime");
      const timezone = view.querySelector("#ProviderTimezone");
      const mpegTs = view.querySelector("#ProviderMpegTs");

      Xtream.fetchJson('Xtream/TestProvider').then(response => {
        status.innerText = response.Status;
        expiry.innerText = response.ExpiryDate;
        cons.innerText = response.ActiveConnections;
        maxCons.innerText = response.MaxConnections;
        time.innerText = response.ServerTime;
        timezone.innerText = response.ServerTimezone;
        mpegTs.innerText = response.SupportsMpegTs;
      }).catch((_) => {
        status.innerText = "Failed. Check server logs.";
        expiry.innerText = "";
        cons.innerText = "";
        maxCons.innerText = "";
        time.innerText = "";
        timezone.innerText = "";
        mpegTs.innerText = "";
      });
    };

    const form = view.querySelector('#XtreamCredentialsForm');
    if (!form.dataset.listenerAttached) {
      loadConfig().then(() => {
        reloadStatus();
      });

      view.querySelector('#AddCredential').addEventListener('click', () => {
        if (!currentConfig.Credentials) {
          currentConfig.Credentials = [];
        }
        currentConfig.Credentials.push({
          Username: '',
          Password: '',
          Label: '',
          IsEnabled: true
        });
        renderAdditionalCredentials(currentConfig.Credentials);
      });

      view.querySelector('#UserAgentFromBrowser').addEventListener('click', (e) => {
        e.preventDefault();
        view.querySelector('#UserAgent').value = navigator.userAgent;
      });

      form.addEventListener('submit', (e) => {
        Dashboard.showLoadingMsg();

        ApiClient.getPluginConfiguration(pluginId).then((config) => {
          config.BaseUrl = view.querySelector('#BaseUrl').value;
          config.Username = view.querySelector('#Username').value;
          config.Password = view.querySelector('#Password').value;
          config.UserAgent = view.querySelector('#UserAgent').value;
          config.IsVodVisible = view.querySelector('#VodVisible').checked;
          config.IsSeriesVisible = view.querySelector('#SeriesVisible').checked;

          // Collect additional credentials data
          const credsList = view.querySelector('#AdditionalCredentialsList');
          config.Credentials = [];
          
          credsList.querySelectorAll('.cred-username').forEach((usernameInput, index) => {
            const passwordInput = credsList.querySelector(`.cred-password[data-index="${index}"]`);
            const labelInput = credsList.querySelector(`.cred-label[data-index="${index}"]`);
            const enabledInput = credsList.querySelector(`.cred-enabled[data-index="${index}"]`);
            
            if (usernameInput && passwordInput) {
              config.Credentials.push({
                Username: usernameInput.value,
                Password: passwordInput.value,
                Label: labelInput ? labelInput.value : '',
                IsEnabled: enabledInput ? enabledInput.checked : true
              });
            }
          });
          
          ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
            currentConfig = config;
            reloadStatus();
            Dashboard.processPluginConfigurationUpdateResult(result);
          });
        });

        e.preventDefault();
        return false;
      });

      form.dataset.listenerAttached = 'true';
    }
  }));
}
