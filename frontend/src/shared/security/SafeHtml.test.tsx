import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { SafeHtml } from "./SafeHtml";
import { safeDocumentUrl } from "./documentLinks";

afterEach(cleanup);
describe("HTML and document link security", () => {
  it.each(["<script>alert(1)</script>", "<img src=x onerror=alert(1)>", "<svg onload=alert(1)>", '<p onclick="alert(1)" style="background:url(https://evil.test)">body</p>', '<a href="javascript:alert(1)">click</a><iframe srcdoc="active"></iframe>'])("removes active content: %s", payload => {
    const { container } = render(<SafeHtml html={`<h1>Document title</h1>${payload}`} />);
    expect(screen.getByRole("heading")).toHaveTextContent("Document title");
    expect(container.querySelector("script,img,svg,iframe,a,[onclick],[style]")).toBeNull();
  });
  it.each(["javascript:alert(1)", "JaVaScRiPt:alert(1)", "data:text/html,attack", "file:///etc/passwd", "vbscript:alert(1)", "//evil.test", "/\\evil.test", "https://user:pass@example.com", "https://example.com\r\n"])("blocks dangerous document URLs: %s", value => expect(safeDocumentUrl(value)).toBeUndefined());
  it("retains HTTPS and authenticated local download paths", () => {
    expect(safeDocumentUrl("https://tenant.sharepoint.com/file.pdf")).toBe("https://tenant.sharepoint.com/file.pdf");
    expect(safeDocumentUrl("/api/sharepoint/download?fileKey=a%2Fb.pdf")).toBe("/api/sharepoint/download?fileKey=a%2Fb.pdf");
  });
});
