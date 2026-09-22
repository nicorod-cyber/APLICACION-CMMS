using Ganss.Xss;

namespace MaintenanceCMMS.Infrastructure.Alerts;

public static class SafeTemplateHtml
{
    public static string Sanitize(string html)
    {
        if (html.Length > 1_000_000) throw new MaintenanceCMMS.Domain.Common.DomainException("La plantilla HTML supera el limite permitido.");
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(["p", "br", "div", "span", "h1", "h2", "h3", "h4", "strong", "b", "em", "i", "u", "small", "ul", "ol", "li", "table", "thead", "tbody", "tr", "th", "td", "hr", "blockquote"]);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(["style", "colspan", "rowspan", "title"]);
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedCssProperties.UnionWith(["color", "background-color", "font-size", "font-weight", "font-family", "text-align", "border", "border-collapse", "padding", "margin", "width"]);
        sanitizer.AllowedSchemes.Clear();
        return sanitizer.Sanitize(html);
    }
}
