import React, { useRef, useEffect, useState } from 'react';
import './EmojiPicker.css';
import { TELEGRAM_REACTIONS } from './reactions';

// لیست کامل ایموجی‌های واکنش (مشابه تلگرام، بدون موارد حساسیت‌برانگیز)
// reactions imported from './reactions'

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
