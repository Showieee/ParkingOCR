/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        euroband: '#003399',
        platebg: '#FFFFFF',
        platedark: '#111827',
      },
      fontFamily: {
        plate: ['"DIN 1451 Mittelschrift"', '"Oswald"', '"FE-Font"', 'sans-serif'],
      }
    },
  },
  plugins: [],
}
