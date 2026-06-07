/** @type {import('tailwindcss').Config} */
export default {
  darkMode: 'class',
  content: [
    './src/BeenThere.Web/Components/**/*.{razor,html}',
    './src/BeenThere.Web/Pages/**/*.{razor,html}'
  ],
  theme: {
    extend: {
      colors: {
        surface: {
          DEFAULT: '#0f1117',
          raised: '#1a1d27',
          overlay: '#22263a',
          border: '#30363d'
        },
        brand: '#58a6ff'
      }
    }
  },
  plugins: []
}
