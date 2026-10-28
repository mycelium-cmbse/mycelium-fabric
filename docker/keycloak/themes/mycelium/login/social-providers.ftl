<#-- Mycelium: identity providers as full-width rows (Figma page 10, "Proto / Sign In", 3761:14936).
     Each row: glyph, display name, protocol line, optional "Last used" (js/mycelium.js), arrow.
     A brand glyph for the social providers Keycloak knows, a letter tile for a generic SAML or OIDC broker.
     The brand colours of the Microsoft mark are the brand's, not Mycelium tokens. -->
<#macro show social>
  <div id="kc-social-providers" class="mycelium-sso">
    <p class="mycelium-sso__intro">${msg("myceliumChooseProvider")}</p>
    <ul class="mycelium-providers">
      <#list social.providers as p>
        <#assign label = (p.displayName?trim?has_content)?then(p.displayName?trim, p.alias)>
        <#switch p.providerId>
          <#case "saml"><#assign protocol="SAML 2.0" kind="saml" host=""><#break>
          <#case "oidc"><#case "keycloak-oidc"><#assign protocol="OIDC" kind="oidc" host=""><#break>
          <#case "microsoft"><#assign protocol="OIDC" kind="brand" host="login.microsoftonline.com"><#break>
          <#case "github"><#assign protocol="OAuth" kind="brand" host="github.com"><#break>
          <#case "gitlab"><#assign protocol="OAuth" kind="brand" host="gitlab.com"><#break>
          <#case "google"><#assign protocol="OIDC" kind="brand" host="accounts.google.com"><#break>
          <#default><#assign protocol="OAuth" kind="oidc" host="">
        </#switch>
        <li>
          <a id="social-${p.alias}" class="mycelium-provider" href="${p.loginUrl}" data-alias="${p.alias}">
            <span class="mycelium-provider__glyph mycelium-provider__glyph--${kind}" aria-hidden="true">
              <#switch p.providerId>
                <#case "microsoft">
                  <svg viewBox="0 0 20 20"><rect x="0" y="0" width="9.5" height="9.5" fill="#F25022"/><rect x="10.5" y="0" width="9.5" height="9.5" fill="#7FBA00"/><rect x="0" y="10.5" width="9.5" height="9.5" fill="#00A4EF"/><rect x="10.5" y="10.5" width="9.5" height="9.5" fill="#FFB900"/></svg>
                  <#break>
                <#case "github">
                  <svg viewBox="0 0 16 16" fill="currentColor"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/></svg>
                  <#break>
                <#case "gitlab">
                  <svg viewBox="0 0 512 512" fill="currentColor"><path d="M29.782 199.732L256 493.714 8.074 309.699c-6.856-5.142-9.712-13.996-7.141-21.993l28.849-87.974zm75.405-174.806c-3.142-8.854-15.709-8.854-18.851 0L29.782 199.732h131.961L105.187 24.926zm56.556 174.806L256 493.714l94.257-293.982H161.743zm349.324 87.974l-28.849-87.974L256 493.714l247.926-184.015c6.855-5.142 9.711-13.996 7.141-21.993zm-85.404-262.78c-3.142-8.854-15.709-8.854-18.851 0l-56.555 174.806h131.961L425.663 24.926z"/></svg>
                  <#break>
                <#case "google">
                  <svg viewBox="0 0 488 512" fill="currentColor"><path d="M488 261.8C488 403.3 391.1 504 248 504 110.8 504 0 393.2 0 256S110.8 8 248 8c66.8 0 123 24.5 166.3 64.9l-67.5 64.9C258.5 52.6 94.3 116.6 94.3 256c0 86.5 69.1 156.6 153.7 156.6 98.2 0 135-70.4 140.8-106.9H248v-85.3h236.1c2.3 12.7 3.9 24.9 3.9 41.4z"/></svg>
                  <#break>
                <#default>
                  ${label[0]?upper_case}
              </#switch>
            </span>
            <span class="mycelium-provider__text">
              <span class="mycelium-provider__name">${label}</span>
              <span class="mycelium-provider__meta">${protocol}<#if host?has_content> · ${host}</#if></span>
            </span>
            <span class="mycelium-provider__last" hidden>${msg("myceliumLastUsed")}</span>
            <svg class="mycelium-provider__arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14"/><path d="m12 5 7 7-7 7"/></svg>
          </a>
        </li>
      </#list>
    </ul>
    <#if realm.password>
      <div class="mycelium-divider" role="separator"><span>${msg("myceliumOr")}</span></div>
    </#if>
  </div>
</#macro>
