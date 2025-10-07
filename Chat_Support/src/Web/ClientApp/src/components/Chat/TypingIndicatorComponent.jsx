import React from 'react';

const TypingIndicatorComponent = ({users}) => {
  if (!users || users.length === 0) return null;

  return (
    <div className="d-flex align-items-center text-muted">
      <div className="typing-indicator me-2">
        <span className="typing-dot"></span>
        <span className="typing-dot"></span>
        <span className="typing-dot"></span>
      </div>
      <small>{users.length === 1 ? `${users[0].userFullName} در حال نوشتن...` : `${users.length} نفر در حال نوشتن...`}</small>
    </div>
  );
};

export default TypingIndicatorComponent;
