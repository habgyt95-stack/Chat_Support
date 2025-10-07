import React, {useState, useEffect} from 'react';
import {Search, X, Send, Users, User} from 'lucide-react';
import {useChat} from '../../hooks/useChat';

const ForwardModal = ({isVisible, onClose, rooms = []}) => {
  const {forwardingMessage, forwardMessage, clearForwardingMessage, currentRoom} = useChat();
  const [selectedRoomIds, setSelectedRoomIds] = useState([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  // Reset state when modal opens/closes
  useEffect(() => {
    if (!isVisible) {
      setSelectedRoomIds([]);
      setSearchQuery('');
      setError('');
    }
  }, [isVisible]);

  // Filter rooms: exclude current room and apply search
  const filteredRooms = rooms.filter((room) => room.id !== currentRoom?.id).filter((room) => room.name.toLowerCase().includes(searchQuery.toLowerCase()));

  const handleRoomSelect = (roomId) => {
    setSelectedRoomIds((prev) => (prev.includes(roomId) ? prev.filter((id) => id !== roomId) : [...prev, roomId]));
  };

  const handleForward = async () => {
    if (selectedRoomIds.length === 0 || !forwardingMessage) return;

    setIsLoading(true);
    setError('');

    try {
      // Forward message to all selected rooms
      for (const roomId of selectedRoomIds) {
        await forwardMessage(forwardingMessage.id, roomId);
      }

      // Success - clear and close
      clearForwardingMessage();
      onClose();
    } catch (err) {
      setError(err.message || 'خطا در هدایت پیام');
    } finally {
      setIsLoading(false);
    }
  };

  const handleClose = () => {
    if (!isLoading) {
      clearForwardingMessage();
      onClose();
    }
  };

  if (!isVisible || !forwardingMessage) return null;

  return (
    <div
      className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50"
      style={{
        position: 'fixed',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.5)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1050,
      }}
    >
      <div
        className="bg-white rounded-lg w-full max-w-md mx-4 max-h-[80vh] flex flex-col"
        style={{
          backgroundColor: 'white',
          borderRadius: '0.5rem',
          width: '100%',
          maxWidth: '28rem',
          margin: '1rem',
          maxHeight: '80vh',
          display: 'flex',
          flexDirection: 'column',
          boxShadow: '0 10px 25px rgba(0, 0, 0, 0.1)',
        }}
      >
        {/* Header */}
        <div
          className="flex items-center justify-between p-4 border-b"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '1rem',
            borderBottom: '1px solid #e5e7eb',
          }}
        >
          <h3 className="text-lg font-semibold" style={{fontSize: '1.125rem', fontWeight: '600'}}>
            هدایت پیام
          </h3>
          <button
            onClick={handleClose}
            disabled={isLoading}
            className="p-1 hover:bg-gray-100 rounded-full transition"
            style={{
              padding: '0.25rem',
              borderRadius: '50%',
              border: 'none',
              background: 'transparent',
              cursor: isLoading ? 'not-allowed' : 'pointer',
              opacity: isLoading ? 0.5 : 1,
            }}
          >
            <X style={{width: '1.25rem', height: '1.25rem'}} />
          </button>
        </div>

        {/* Message Preview */}
        <div
          style={{
            margin: '1rem',
            padding: '0.75rem',
            backgroundColor: '#f3f4f6',
            borderRadius: '0.375rem',
            borderRight: '3px solid #3b82f6',
          }}
        >
          <div style={{fontSize: '0.875rem', color: '#6b7280', marginBottom: '0.25rem'}}>پیام از: {forwardingMessage.senderFullName}</div>
          <div
            style={{
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {forwardingMessage.content}
          </div>
        </div>

        {/* Error Alert */}
        {error && (
          <div
            style={{
              margin: '0 1rem',
              padding: '0.75rem',
              backgroundColor: '#fee2e2',
              color: '#991b1b',
              borderRadius: '0.375rem',
              fontSize: '0.875rem',
            }}
          >
            {error}
          </div>
        )}

        {/* Search */}
        <div className="p-4 border-b" style={{padding: '1rem', borderBottom: '1px solid #e5e7eb'}}>
          <div className="relative" style={{position: 'relative'}}>
            <Search
              style={{
                position: 'absolute',
                right: '0.75rem',
                top: '50%',
                transform: 'translateY(-50%)',
                color: '#9ca3af',
                width: '1.25rem',
                height: '1.25rem',
              }}
            />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="جستجوی چت..."
              style={{
                width: '100%',
                paddingRight: '2.5rem',
                paddingLeft: '0.75rem',
                paddingTop: '0.5rem',
                paddingBottom: '0.5rem',
                border: '1px solid #d1d5db',
                borderRadius: '0.375rem',
                fontSize: '0.875rem',
                outline: 'none',
              }}
            />
          </div>
        </div>

        {/* Chat List */}
        <div className="flex-1 overflow-y-auto" style={{flex: 1, overflowY: 'auto'}}>
          {filteredRooms.length === 0 ? (
            <div
              style={{
                textAlign: 'center',
                padding: '3rem 1rem',
                color: '#6b7280',
              }}
            >
              چتی برای هدایت یافت نشد
            </div>
          ) : (
            filteredRooms.map((room) => (
              <label
                key={room.id}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  padding: '0.75rem 1rem',
                  cursor: 'pointer',
                  transition: 'background-color 0.2s',
                }}
                onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = '#f9fafb')}
                onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
              >
                <input type="checkbox" checked={selectedRoomIds.includes(room.id)} onChange={() => handleRoomSelect(room.id)} style={{marginLeft: '0.75rem'}} />
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    flex: 1,
                  }}
                >
                  <div
                    style={{
                      width: '2.5rem',
                      height: '2.5rem',
                      borderRadius: '50%',
                      backgroundColor: '#e5e7eb',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      marginLeft: '0.75rem',
                    }}
                  >
                    {room.isGroup ? <Users style={{width: '1.25rem', height: '1.25rem', color: '#6b7280'}} /> : <User style={{width: '1.25rem', height: '1.25rem', color: '#6b7280'}} />}
                  </div>
                  <div>
                    <div style={{fontWeight: '500'}}>{room.name}</div>
                    {room.lastMessageContent && (
                      <div
                        style={{
                          fontSize: '0.875rem',
                          color: '#6b7280',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          whiteSpace: 'nowrap',
                          maxWidth: '15rem',
                        }}
                      >
                        {room.lastMessageContent}
                      </div>
                    )}
                  </div>
                </div>
              </label>
            ))
          )}
        </div>

        {/* Footer */}
        <div
          style={{
            padding: '1rem',
            borderTop: '1px solid #e5e7eb',
          }}
        >
          <button
            onClick={handleForward}
            disabled={selectedRoomIds.length === 0 || isLoading}
            style={{
              width: '100%',
              backgroundColor: selectedRoomIds.length === 0 || isLoading ? '#d1d5db' : '#3b82f6',
              color: 'white',
              padding: '0.5rem 1rem',
              borderRadius: '0.375rem',
              border: 'none',
              cursor: selectedRoomIds.length === 0 || isLoading ? 'not-allowed' : 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '0.5rem',
              fontSize: '0.875rem',
              fontWeight: '500',
            }}
          >
            {isLoading ? (
              <div
                style={{
                  width: '1.25rem',
                  height: '1.25rem',
                  border: '2px solid white',
                  borderTopColor: 'transparent',
                  borderRadius: '50%',
                  animation: 'spin 0.8s linear infinite',
                }}
              />
            ) : (
              <>
                <Send style={{width: '1rem', height: '1rem'}} />
                هدایت به {selectedRoomIds.length} چت
              </>
            )}
          </button>
        </div>
      </div>

      <style>{`
        @keyframes spin {
          to { transform: rotate(360deg); }
        }
      `}</style>
    </div>
  );
};

export default ForwardModal;
