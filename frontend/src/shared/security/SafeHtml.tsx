import DOMPurify from "dompurify";

// Both notifications and previews intentionally retain document formatting.
// No URLs, images, CSS, forms, SVG or executable attributes are accepted.
export function SafeHtml({ html, className }: { html: string; className?: string }) {
  const clean = DOMPurify.sanitize(html, {
    ALLOWED_TAGS: ["p", "br", "div", "span", "h1", "h2", "h3", "h4", "strong", "b", "em", "i", "u", "small", "ul", "ol", "li", "table", "thead", "tbody", "tr", "th", "td", "hr", "blockquote"],
    ALLOWED_ATTR: ["colspan", "rowspan", "title"],
    ALLOW_DATA_ATTR: false,
    ALLOW_ARIA_ATTR: false
  });
  return <div className={className} dangerouslySetInnerHTML={{ __html: clean }} />;
}
