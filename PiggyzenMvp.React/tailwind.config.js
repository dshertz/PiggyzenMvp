/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#f5f7ff',
          100: '#e3e9ff',
          200: '#c3cdfd',
          300: '#9eabfa',
          400: '#7582f5',
          500: '#5362f1',
          600: '#3e49dc',
          700: '#3038b0',
          800: '#262d8a',
          900: '#1f266d'
        }
      }
    }
  },
  plugins: []
};
