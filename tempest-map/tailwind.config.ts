import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./app/**/*.{ts,tsx}",
    "./components/**/*.{ts,tsx}",
    "./lib/**/*.{ts,tsx}"
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          DEFAULT: "#33d2ff",
          50: "#ecfeff",
          100: "#cff9ff",
          200: "#a5f3ff",
          300: "#70e9ff",
          400: "#33d2ff",
          500: "#0ab4e6",
          600: "#008fc4",
          700: "#016f98",
          800: "#085a7a",
          900: "#0b4b64"
        }
      }
    }
  },
  plugins: []
};

export default config;
