<#-- Mycelium email layout: mark and product name, a white card, a quiet footer. Table layout and inline styles,
     because that is what email clients render. Colours come from theme.properties (the tokens, spelled out). -->
<#import "mark.ftl" as brand>
<#assign font = "Inter, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif">

<#macro emailLayout>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>${msg("myceliumProductName")}</title>
</head>
<body style="margin:0;padding:0;background:${properties.myceliumColourBackground};">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:${properties.myceliumColourBackground};">
    <tr>
        <td align="center" style="padding:32px 16px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;">
                <tr>
                    <td style="padding:0 0 24px;font:600 20px/28px ${font};color:${properties.myceliumColourForeground};">
                        <@brand.mark colour=properties.myceliumColourPrimary/>&nbsp;&nbsp;${msg("myceliumProductName")}
                    </td>
                </tr>
                <tr>
                    <td style="padding:32px;background:${properties.myceliumColourCard};border:1px solid ${properties.myceliumColourBorder};border-radius:12px;font:400 14px/22px ${font};color:${properties.myceliumColourForeground};">
                        <#nested>
                    </td>
                </tr>
                <tr>
                    <td style="padding:24px 0 0;font:400 12px/18px ${font};color:${properties.myceliumColourMutedForeground};text-align:center;">
                        ${msg("myceliumEmailFooter")}
                    </td>
                </tr>
            </table>
        </td>
    </tr>
</table>
</body>
</html>
</#macro>

<#macro button href label>
<table role="presentation" cellpadding="0" cellspacing="0" style="margin:24px 0;">
    <tr>
        <td style="background:${properties.myceliumColourPrimary};border-radius:8px;">
            <a href="${href}" style="display:inline-block;padding:10px 20px;font:600 14px/20px ${font};color:${properties.myceliumColourPrimaryForeground};text-decoration:none;">${label}</a>
        </td>
    </tr>
</table>
</#macro>

<#macro muted>
<p style="margin:0 0 8px;color:${properties.myceliumColourMutedForeground};font-size:13px;line-height:20px;"><#nested></p>
</#macro>
