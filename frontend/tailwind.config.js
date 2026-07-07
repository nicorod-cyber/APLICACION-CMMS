export default {
    darkMode: "class",
    content: ["./index.html", "./src/**/*.{ts,tsx}"],
    theme: {
        extend: {
            fontFamily: {
                sans: ["Inter", "ui-sans-serif", "system-ui", "sans-serif"]
            },
            boxShadow: {
                panel: "0 18px 40px rgba(15, 23, 42, 0.08)"
            }
        }
    },
    plugins: []
};
