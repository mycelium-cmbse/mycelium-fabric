<#import "template.ftl" as layout>
<@layout.emailLayout>
<p style="margin:0 0 8px;">${msg("myceliumResetIntro")}</p>
<@layout.button href=link label=msg("myceliumResetAction")/>
<@layout.muted>${msg("myceliumLinkExpiry", linkExpirationFormatter(linkExpiration))}</@layout.muted>
<@layout.muted>${msg("myceliumResetIgnore")}</@layout.muted>
</@layout.emailLayout>
