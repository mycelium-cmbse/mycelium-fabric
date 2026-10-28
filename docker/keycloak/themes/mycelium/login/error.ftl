<#-- Mycelium: Auth error (Figma page 10, "Proto / Auth error", 6418:16).
     Centred warning badge, title, the message Keycloak gives, then "Try again" and "Back to application".
     The badge is a Lucide TriangleAlert in currentColor, coloured by the warning tokens in mycelium.css. -->
<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=false bodyClass="mycelium-error"; section>
    <#if section = "header">
        <span class="mycelium-error__badge" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3"/><path d="M12 9v4"/><path d="M12 17h.01"/></svg></span>
        <span class="mycelium-error__title">${kcSanitize(msg("errorTitle"))?no_esc}</span>
    <#elseif section = "form">
        <div id="kc-error-message">
            <p class="instruction">${kcSanitize(message.summary)?no_esc}</p>
            <#-- "Try again" has two honest targets.
                 1. The sign-in in progress, restarted in place (keeps the original redirect): only when
                    Keycloak tells us one exists, i.e. it put tab_id in its URLs
                    (FreeMarkerLoginFormsProvider.prepareBaseUriBuilder, Keycloak 26.0). `auth` is always
                    set, so it cannot tell. The application then stays reachable as a secondary action.
                 2. Otherwise the application itself, which starts a fresh sign-in and resumes the page the
                    user asked for (SSS-CC-LINK-E3M). The restart URL is not used blind: with no
                    authentication cookie it answers "Cookie not found" (an error raised before any
                    sign-in started, such as a bad redirect_uri, is in that case). -->
            <#assign canRestart = url.loginRestartFlowUrl?has_content && url.loginRestartFlowUrl?contains("tab_id=")>
            <#assign hasApplication = !skipLink?? && client?? && client.baseUrl?has_content>
            <#if canRestart>
                <#assign tryAgainUrl = url.loginRestartFlowUrl>
            <#elseif hasApplication>
                <#assign tryAgainUrl = client.baseUrl>
            </#if>
            <#if tryAgainUrl??>
            <div class="mycelium-actions">
                <a id="tryAgain" class="${properties.kcButtonPrimaryClass} ${properties.kcButtonBlockClass}" href="${tryAgainUrl}">${msg("myceliumTryAgain")}</a>
                <#if canRestart && hasApplication>
                    <a id="backToApplication" class="${properties.kcButtonSecondaryClass} ${properties.kcButtonBlockClass}" href="${client.baseUrl}">${msg("backToApplication")}</a>
                </#if>
            </div>
            </#if>
        </div>
    </#if>
</@layout.registrationLayout>
