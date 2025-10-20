import React, { useRef, useEffect, useState } from 'react';
import './EmojiPicker.css';

// لیست کامل ایموجی‌های واکنش (مشابه تلگرام، بدون موارد حساسیت‌برانگیز)
const TELEGRAM_REACTIONS = [
  // دسته اول: احساسات مثبت
  { emoji: '👍', category: 'positive' },
  { emoji: '👎', category: 'positive' },
  { emoji: '❤️', category: 'positive' },
  { emoji: '🔥', category: 'positive' },
  { emoji: '🥰', category: 'positive' },
  { emoji: '👏', category: 'positive' },
  { emoji: '😁', category: 'positive' },
  { emoji: '🤔', category: 'positive' },
  { emoji: '🤯', category: 'positive' },
  { emoji: '😱', category: 'positive' },
  { emoji: '🎉', category: 'positive' },
  { emoji: '🤩', category: 'positive' },
  { emoji: '🤗', category: 'positive' },
  { emoji: '💯', category: 'positive' },
  { emoji: '🤝', category: 'positive' },
  { emoji: '⚡', category: 'positive' },
  { emoji: '🏆', category: 'positive' },
  { emoji: '🕊', category: 'positive' },
  { emoji: '👌', category: 'positive' },
  { emoji: '🆒', category: 'positive' },
  
  // دسته دوم: خنده و شادی
  { emoji: '😂', category: 'laugh' },
  { emoji: '😆', category: 'laugh' },
  { emoji: '😄', category: 'laugh' },
  { emoji: '😃', category: 'laugh' },
  { emoji: '🤣', category: 'laugh' },
  
  // دسته سوم: تعجب
  { emoji: '😮', category: 'surprise' },
  { emoji: '😯', category: 'surprise' },
  { emoji: '😲', category: 'surprise' },
  { emoji: '🤐', category: 'surprise' },
  
  // دسته چهارم: ناراحتی و غمگینی
  { emoji: '😢', category: 'sad' },
  { emoji: '😭', category: 'sad' },
  { emoji: '😔', category: 'sad' },
  { emoji: '😞', category: 'sad' },
  { emoji: '🥺', category: 'sad' },
  { emoji: '😕', category: 'sad' },
  
  // دسته پنجم: عصبانیت
  { emoji: '😠', category: 'angry' },
  { emoji: '😡', category: 'angry' },
  
  // دسته ششم: دعا و امید
  { emoji: '🙏', category: 'pray' },
  { emoji: '🤲', category: 'pray' },
  
  // دسته هفتم: متفرقه
  { emoji: '🤝', category: 'misc' },
  { emoji: '💪', category: 'misc' },
  { emoji: '👀', category: 'misc' },
  { emoji: '🤡', category: 'misc' },
  { emoji: '👻', category: 'misc' },
  { emoji: '💀', category: 'misc' },
  { emoji: '🤖', category: 'misc' },
  { emoji: '👽', category: 'misc' },
  { emoji: '🌚', category: 'misc' },
  { emoji: '🌝', category: 'misc' },
  { emoji: '🌞', category: 'misc' },
  { emoji: '⭐', category: 'misc' },
  { emoji: '✨', category: 'misc' },
  { emoji: '💫', category: 'misc' },
];

const EmojiPicker = ({ onSelect, onClose, position }) => {
  const pickerRef = useRef(null);
  const [selectedIndex, setSelectedIndex] = useState(-1);

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (pickerRef.current && !pickerRef.current.contains(event.target)) {
        onClose();
      }
    };

    const handleEscape = (event) => {
      if (event.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    document.addEventListener('keydown', handleEscape);
    
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleEscape);
    };
  }, [onClose]);

  const handleSelect = (emoji) => {
    onSelect(emoji);
    onClose();
  };

  return (
    <div 
      ref={pickerRef} 
      className="emoji-picker-modern"
      style={position}
    >
      <div className="emoji-picker-header">
        <span>واکنش‌ها</span>
      </div>
      <div className="emoji-picker-grid">
        {TELEGRAM_REACTIONS.map((item, index) => (
          <button
            key={index}
            className={`emoji-item ${selectedIndex === index ? 'selected' : ''}`}
            onClick={() => handleSelect(item.emoji)}
            onMouseEnter={() => setSelectedIndex(index)}
            type="button"
            aria-label={`React with ${item.emoji}`}
          >
            {item.emoji}
          </button>
        ))}
      </div>
    </div>
  );
};

export default EmojiPicker;
