<#import "template.ftl" as layout>
<@layout.emailLayout>
<p style="margin:0 0 8px;">${msg("myceliumVerifyIntro")}</p>
<@layout.button href=link label=msg("myceliumVerifyAction")/>
<@layout.muted>${msg("myceliumLinkExpiry", linkExpirationFormatter(linkExpiration))}</@layout.muted>
<@layout.muted>${msg("myceliumVerifyIgnore")}</@layout.muted>
</@layout.emailLayout>
