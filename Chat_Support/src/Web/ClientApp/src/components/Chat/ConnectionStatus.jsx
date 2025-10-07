import React from 'react';
import {Badge} from 'react-bootstrap';

const ConnectionStatus = ({isConnected}) => {
  return (
    <Badge bg={isConnected ? 'success' : 'danger'} className="d-flex align-items-center gap-1">
      <span className={`rounded-circle ${isConnected ? 'online-indicator' : ''}`} style={{width: '8px', height: '8px', backgroundColor: 'white'}}></span>
      {isConnected ? 'متصل' : 'قطع'}
    </Badge>
  );
};

export default ConnectionStatus;
