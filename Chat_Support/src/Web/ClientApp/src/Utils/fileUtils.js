// src/utils/fileUtils.js

/**
 * تبدیل حجم فایل از بایت به فرمت خوانا
 * @param {number} bytes - حجم فایل به بایت
 * @returns {string} - حجم فایل به فرمت خوانا
 */
export const formatFileSize = (bytes) => {
    if (!bytes || bytes === 0) return '0 بایت';
    const k = 1024;
    const sizes = ['بایت', 'کیلوبایت', 'مگابایت', 'گیگابایت', 'ترابایت'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  };
  
  /**
   * تبدیل تاریخ به فرمت فارسی
   * @param {string|Date} dateInput - تاریخ ورودی
   * @returns {string} - تاریخ فرمت شده
   */
  export const formatPersianDate = (dateInput) => {
    try {
      const date = typeof dateInput === 'string' ? new Date(dateInput) : dateInput;
      return new Intl.DateTimeFormat('fa-IR', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      }).format(date);
    } catch (error) {
      console.error('Error formatting date:', error);
      return dateInput?.toString() || '';
    }
  };
  
  /**
   * بررسی می‌کند آیا فایل تصویر است یا خیر
   * @param {string} fileName - نام فایل
   * @returns {boolean} - true اگر فایل تصویر باشد
   */
  export const isImageFile = (fileName) => {
    if (!fileName) return false;
    const imageExtensions = ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.svg', '.bmp'];
    const ext = fileName.substring(fileName.lastIndexOf('.')).toLowerCase();
    return imageExtensions.includes(ext);
  };
  
  /**
   * بررسی می‌کند آیا فایل PDF است یا خیر
   * @param {string} fileName - نام فایل
   * @returns {boolean} - true اگر فایل PDF باشد
   */
  export const isPdfFile = (fileName) => {
    if (!fileName) return false;
    return fileName.toLowerCase().endsWith('.pdf');
  };
  
  /**
   * بررسی می‌کند آیا فایل قابل پیش‌نمایش است یا خیر
   * @param {string} fileName - نام فایل
   * @returns {boolean} - true اگر فایل قابل پیش‌نمایش باشد
   */
  export const isPreviewableFile = (fileName) => {
    if (!fileName) return false;
    return isImageFile(fileName) || isPdfFile(fileName);
  };
  
  /**
   * بررسی اندازه فایل
   * @param {File} file - فایل
   * @param {number} maxSizeMB - حداکثر اندازه به مگابایت
   * @returns {boolean} - true اگر فایل کوچکتر از حد مجاز باشد
   */
  export const validateFileSize = (file, maxSizeMB = 50) => {
    return file.size <= maxSizeMB * 1024 * 1024;
  };
  
  /**
   * بررسی نوع فایل
   * @param {File} file - فایل
   * @param {Array} allowedTypes - انواع مجاز فایل
   * @returns {boolean} - true اگر نوع فایل مجاز باشد
   */
  export const validateFileType = (file, allowedTypes = []) => {
    if (!allowedTypes || allowedTypes.length === 0) return true;
    
    const fileType = file.type.split('/')[0];
    const fileExtension = file.name.split('.').pop().toLowerCase();
    
    return allowedTypes.includes(fileType) || allowedTypes.includes(fileExtension);
  };
  
  /**
   * دریافت آیکون مناسب برای نوع فایل
   * @param {string} fileName - نام فایل
   * @returns {object} - آبجکت حاوی آیکون و رنگ
   */
  export const getFileIcon = (fileName) => {
    // TODO: implement based on icon library (placeholder to avoid unused warning)
    void fileName;
  };
  
  /**
   * تولید نام امن برای فایل
   * @param {string} fileName - نام فایل اصلی
   * @returns {string} - نام امن فایل
   */
  export const generateSafeFileName = (fileName) => {
    if (!fileName) return `file-${Date.now()}`;
    
    // جدا کردن پسوند فایل
    const lastDotIndex = fileName.lastIndexOf('.');
    const extension = lastDotIndex !== -1 ? fileName.substring(lastDotIndex) : '';
    const baseName = lastDotIndex !== -1 ? fileName.substring(0, lastDotIndex) : fileName;
    
    // حذف کاراکترهای غیرمجاز
    const safeBaseName = baseName
      .replace(/[^\w\s.-]/g, '') // حذف کاراکترهای خاص
      .replace(/\s+/g, '-'); // تبدیل فاصله به خط تیره
    
    // اضافه کردن تایم استمپ برای یکتا بودن
    return `${safeBaseName}-${Date.now()}${extension}`;
  };
  
  /**
   * ایجاد URI داده برای فایل
   * @param {Blob} blob - فایل بلاب
   * @returns {Promise<string>} - URI داده
   */
  export const blobToDataURI = (blob) => {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result);
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(blob);
    });
  };
  
  /**
   * استخراج متادیتای تصویر
   * @param {File} imageFile - فایل تصویر
   * @returns {Promise<object>} - متادیتای تصویر
   */
  export const getImageMetadata = (imageFile) => {
    return new Promise((resolve) => {
      const img = new Image();
      img.onload = () => {
        resolve({
          width: img.width,
          height: img.height,
          aspectRatio: img.width / img.height,
        });
      };
      img.onerror = () => {
        resolve({ width: 0, height: 0, aspectRatio: 0 });
      };
      img.src = URL.createObjectURL(imageFile);
    });
  };
  
  /**
   * فشرده‌سازی تصویر
   * @param {File} imageFile - فایل تصویر
   * @param {object} options - گزینه‌های فشرده‌سازی
   * @returns {Promise<Blob>} - فایل فشرده‌شده
   */
  export const compressImage = (imageFile, options = { maxWidth: 1600, quality: 0.8, format: 'jpeg' }) => {
    return new Promise((resolve, reject) => {
      const img = new Image();
      img.onload = () => {
        // محاسبه ابعاد جدید
        let width = img.width;
        let height = img.height;
        
        if (width > options.maxWidth) {
          height = Math.round((height * options.maxWidth) / width);
          width = options.maxWidth;
        }
        
        // رسم تصویر در canvas
        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(img, 0, 0, width, height);
        
        // تبدیل به blob
        canvas.toBlob(
          (blob) => {
            if (blob) {
              // ایجاد فایل جدید با نام اصلی
              const compressedFile = new File([blob], imageFile.name, {
                type: `image/${options.format}`,
                lastModified: Date.now(),
              });
              resolve(compressedFile);
            } else {
              reject(new Error('فشرده‌سازی تصویر با خطا مواجه شد.'));
            }
          },
          `image/${options.format}`,
          options.quality
        );
      };
      img.onerror = () => {
        reject(new Error('خطا در بارگذاری تصویر برای فشرده‌سازی.'));
      };
      img.src = URL.createObjectURL(imageFile);
    });
  };

  /**
   * دانلود ساده فایل از یک URL با تعیین نام دلخواه (الهام گرفته از تجربه تلگرام)
   * - اگر fetch موفق نباشد، لینک به صورت fallback باز می‌شود.
   * - Blob استفاده می‌شود تا هدرهای Content-Disposition نادیده گرفته نشود.
   * @param {string} url - آدرس فایل
   * @param {string} filename - نام فایل مقصد (می‌تواند شامل پسوند باشد)
   */
  export const downloadFile = async (url, filename = '') => {
    if (!url) return;
    try {
      const res = await fetch(url, { credentials: 'include' });
      if (!res.ok) throw new Error('HTTP ' + res.status);
      const blob = await res.blob();
      // اگر پسوند ندارد، از نوع MIME حدس می‌زنیم
      if (filename && !/\.[a-zA-Z0-9]{2,6}$/.test(filename) && blob.type) {
        const extMap = { 'image/jpeg': '.jpg', 'image/png': '.png', 'image/gif': '.gif', 'video/mp4': '.mp4', 'audio/mpeg': '.mp3', 'audio/wav': '.wav', 'application/pdf': '.pdf' };
        const guessed = extMap[blob.type];
        if (guessed) filename += guessed;
      }
      if (!filename) {
        // استخراج از URL
        try {
          const u = new URL(url, window.location.origin);
          filename = decodeURIComponent(u.pathname.split('/').pop() || 'file');
        } catch {
          filename = 'file';
        }
      }
      const objectUrl = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = objectUrl;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(objectUrl), 1500);
    } catch (err) {
      console.error('downloadFile error:', err);
      // fallback
      const a = document.createElement('a');
      a.href = url;
      a.target = '_blank';
      a.rel = 'noopener noreferrer';
      a.click();
    }
  };