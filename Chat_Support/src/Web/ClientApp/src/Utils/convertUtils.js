const englishDigits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];
const farsiDigits = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'];
/**
 * تابعی برای تبدیل رشته‌ای از اعداد بین فارسی و انگلیسی
 * @param {string} input - رشته حاوی اعداد
 * @param {string} target - نوع تبدیل: 'farsi' یا 'english'
 * @returns {string} - رشته تبدیل شده
 */
export const convertNumbers = (input, target) => {
  const sourceDigits = target === 'farsi' ? englishDigits : farsiDigits;
  const targetDigits = target === 'farsi' ? farsiDigits : englishDigits;
  const inputString = String(input);
  return inputString
    .split('')
    .map((char) => targetDigits[sourceDigits.indexOf(char)] || char)
    .join('');
};
/**
 * تابعی برای تبدیل تاریخ به تاریخ فارسی
 * @param {string} date - تاریخ به فرمت 'yyyy/mm/dd'
 * @returns {string} - تاریخ به عدد فارسی
 */
export const convertDateToFarsi = (date) => {
  const [year, month, day] = date.split('/');
  return `${convertNumbers(year, 'farsi')}/${convertNumbers(month, 'farsi')}/${convertNumbers(day, 'farsi')}`;
};

/**
 * تابعی برای تبدیل تاریخ فارسی به انگلیسی
 * @param {string} date - تاریخ به فرمت 'yyyy/mm/dd' با اعداد فارسی
 * @returns {string} - تاریخ به عدد انگلیسی
 */
export const convertDateToEnglish = (date) => {
  const [year, month, day] = date.split('/');
  return `${convertNumbers(year, 'english')}/${convertNumbers(month, 'english')}/${convertNumbers(day, 'english')}`;
};
/**
 * تابعی برای تبدیل رشته به عدد
 * @param {string} value - رشته
 * @returns {int} - عدد یا null
 */
export const convertToIntOrNull = (value) => {
  const parsed = parseInt(value, 10);
  return isNaN(parsed) ? null : parsed;
};
/**
 * تابعی برای فرمت کردن رشته عددی به‌طوری که در قسمت صحیح هر ۳ رقم با کاما جدا شوند.
 * در صورت وجود قسمت اعشاری، آن را بدون تغییر برمی‌گرداند.
 *
 * @param {string} numStr - رشته عددی ورودی که ممکن است شامل قسمت اعشاری باشد.
 * @returns {string} - رشته فرمت شده با جداکننده کاما در قسمت صحیح.
 */
export const formatBigNumber = (num) => {
  if (num === null || num === undefined || num === '') return '';
  const numStr = String(num);
  if (isNaN(Number(numStr))) return numStr;
  let [intPart, decimalPart] = numStr.split('.');
  intPart = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
  return decimalPart !== undefined ? `${intPart}.${decimalPart}` : intPart;
};
