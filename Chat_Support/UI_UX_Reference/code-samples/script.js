// Shared JavaScript across all pages

// Sample Data - In real app, this would come from API
const sampleData = {
    users: [
        {
            id: 1,
            name: "محمد رضایی",
            email: "mohammad@example.com",
            phone: "09123456789",
            registrationDate: "1402/10/15",
            lastSeen: "2 ساعت پیش",
            avatar: "http://static.photos/people/200x200/1",
            chats: [
                {
                    id: 101,
                    title: "پشتیبانی محصول",
                    lastMessage: "مشکل محصول حل شد؟",
                    unreadCount: 2,
                    createdAt: "1402/11/20",
                    messages: [
                        { id: 1001, text: "سلام، من مشکل در استفاده از محصول دارم", sender: "user", timestamp: "14:30" },
                        { id: 1002, text: "سلام محمد جان، چه کمکی میتونم بکنم؟", sender: "admin", timestamp: "14:32" },
                        { id: 1003, text: "نمیتونم از قسمت گزارش‌گیری استفاده کنم", sender: "user", timestamp: "14:35" }
                    ]
                },
                {
                    id: 102,
                    title: "درخواست ویژگی جدید",
                    lastMessage: "ویژگی اضافه خواهد شد",
                    unreadCount: 0,
                    createdAt: "1402/11/18",
                    messages: [
                        { id: 2001, text: "سلام، امکان اضافه کردن گزارش پیشرفته هست؟", sender: "user", timestamp: "10:15" },
                        { id: 2002, text: "بله، در نسخه بعدی اضافه میشه", sender: "admin", timestamp: "10:20" }
                    ]
                }
            ]
        },
        {
            id: 2,
            name: "فاطمه احمدی",
            email: "fatemeh@example.com",
            phone: "09129876543",
            registrationDate: "1402/09/22",
            lastSeen: "5 دقیقه پیش",
            avatar: "http://static.photos/people/200x200/2",
            chats: [
                {
                    id: 201,
                    title: "مشکل فنی",
                    lastMessage: "خطا رفع شد",
                    unreadCount: 1,
                    createdAt: "1402/11/21",
                    messages: [
                        { id: 3001, text: "با سلام، هنگام لاگین خطا میگیرم", sender: "user", timestamp: "09:45" },
                        { id: 3002, text: "لطفا شماره خطا رو بفرستید", sender: "admin", timestamp: "09:50" }
                    ]
                }
            ]
        },
        {
            id: 3,
            name: "علی کریمی",
            email: "ali@example.com",
            phone: "09121234567",
            registrationDate: "1402/11/05",
            lastSeen: "1 روز پیش",
            avatar: "http://static.photos/people/200x200/3",
            chats: [
                {
                    id: 301,
                    title: "سوال درباره قیمت‌ها",
                    lastMessage: "اطلاعات ارسال شد",
                    unreadCount: 0,
                    createdAt: "1402/11/19",
                    messages: [
                        { id: 4001, text: "سلام، قیمت پلن حرفه‌ای چنده؟", sender: "user", timestamp: "16:20" },
                        { id: 4002, text: "پلن حرفه‌ای ماهانه ۲۹۹ هزار تومانه", sender: "admin", timestamp: "16:25" }
                    ]
                }
            ]
        }
    ]
};

// Global State
let currentState = {
    selectedUser: null,
    selectedChat: null,
    users: []
};

// Initialize the application
document.addEventListener('DOMContentLoaded', function() {
    initializeApp();
    setupEventListeners();
});

function initializeApp() {
    // Load sample data
    currentState.users = sampleData.users;
    renderUsersList();
    
    // Show welcome message
    showToast('داشبورد مدیریت گفتگوها بارگذاری شد', 'success');
}

function setupEventListeners() {
    // Search functionality
    const searchInput = document.querySelector('input[placeholder="جستجو کاربر..."]');
    if (searchInput) {
        searchInput.addEventListener('input', debounce(handleSearch, 300));
    }
}

function renderUsersList() {
    const usersList = document.getElementById('usersList');
    if (!usersList) return;

    usersList.innerHTML = currentState.users.map(user => `
        <div class="user-card p-4 hover:bg-gray-50 transition-all duration-200 ${currentState.selectedUser?.id === user.id ? 'active' : ''}" 
             data-user-id="${user.id}" 
             onclick="selectUser(${user.id})">
            <div class="flex items-center gap-3">
                <img src="${user.avatar}" alt="${user.name}" class="w-12 h-12 rounded-full object-cover">
                <div class="flex-1">
                    <h3 class="font-semibold text-gray-800">${user.name}</h3>
                    <p class="text-sm text-gray-600">${user.email}</p>
                    <div class="flex items-center gap-4 mt-1 text-xs text-gray-500">
                        <span class="flex items-center gap-1">
                            <i data-feather="phone" class="w-3 h-3"></i>
                            ${user.phone}
                        </span>
                        <span class="flex items-center gap-1">
                            <i data-feather="clock" class="w-3 h-3"></i>
                            آخرین بازدید: ${user.lastSeen}
                        </span>
                    </div>
                </div>
                <div class="text-left">
                    <span class="text-xs text-gray-500">${user.registrationDate}</span>
                </div>
            </div>
        </div>
    `).join('');

    feather.replace();
}

function selectUser(userId) {
    const user = currentState.users.find(u => u.id === userId);
    if (!user) return;

    currentState.selectedUser = user;
    currentState.selectedChat = null;
    
    // Update UI
    renderUsersList();
    renderUserChats();
    clearChatMessages();
    
    showToast(`گفتگوهای ${user.name} بارگذاری شد`, 'info');
}

function renderUserChats() {
    const userChats = document.getElementById('userChats');
    if (!userChats || !currentState.selectedUser) return;

    userChats.innerHTML = currentState.selectedUser.chats.map(chat => `
        <div class="chat-card p-4 hover:bg-gray-50 transition-all duration-200 ${currentState.selectedChat?.id === chat.id ? 'active' : ''}" 
             data-chat-id="${chat.id}" 
             onclick="selectChat(${chat.id})">
            <div class="flex items-center justify-between">
                <div class="flex-1">
                    <h4 class="font-medium text-gray-800">${chat.title}</h4>
                    <p class="text-sm text-gray-600 mt-1">${chat.lastMessage}</p>
                    <div class="flex items-center gap-4 mt-2 text-xs text-gray-500">
                        <span class="flex items-center gap-1">
                            <i data-feather="calendar" class="w-3 h-3"></i>
                        ایجاد: ${chat.createdAt}
                        </span>
                    </div>
                </div>
                ${chat.unreadCount > 0 ? `
                    <span class="bg-danger text-white rounded-full w-6 h-6 flex items-center justify-center text-xs">
                        ${chat.unreadCount}
                    </span>
                ` : ''}
            </div>
        </div>
    `).join('');

    feather.replace();
}

function selectChat(chatId) {
    if (!currentState.selectedUser) return;

    const chat = currentState.selectedUser.chats.find(c => c.id === chatId);
    if (!chat) return;

    currentState.selectedChat = chat;
    
    // Update UI
    renderUserChats();
    renderChatMessages();
    
    showToast(`پیام‌های ${chat.title} بارگذاری شد`, 'info');
}

function renderChatMessages() {
    const chatMessages = document.getElementById('chatMessages');
    if (!chatMessages || !currentState.selectedChat) return;

    chatMessages.innerHTML = currentState.selectedChat.messages.map(message => `
        <div class="message-bubble fade-in ${message.sender === 'user' ? 'message-received' : 'message-sent'}">
            <div class="flex items-start gap-2">
                ${message.sender === 'user' ? `
                    <img src="${currentState.selectedUser.avatar}" alt="${currentState.selectedUser.name}" class="w-8 h-8 rounded-full object-cover mt-1">
                ` : ''}
                <div class="flex-1">
                    <p class="text-sm leading-relaxed">${message.text}</p>
                    <span class="text-xs opacity-70 mt-1 block">${message.timestamp}</span>
                </div>
                ${message.sender === 'admin' ? `
                    <img src="http://static.photos/office/200x200/1" alt="مدیر" class="w-8 h-8 rounded-full object-cover mt-1">
                ` : ''}
            </div>
        </div>
    `).join('');
}

function clearChatMessages() {
    const chatMessages = document.getElementById('chatMessages');
    if (chatMessages) {
        chatMessages.innerHTML = `
            <div class="text-center py-8 text-gray-500">
                <i data-feather="message-circle" class="w-12 h-12 mx-auto mb-4"></i>
            <p>یک گفتگو را انتخاب کنید تا پیام‌ها نمایش داده شوند</p>
            </div>
        `;
        feather.replace();
    }
}

function handleSearch(event) {
    const searchTerm = event.target.value.toLowerCase();
    
    if (searchTerm.trim() === '') {
        currentState.users = sampleData.users;
    } else {
        currentState.users = sampleData.users.filter(user => 
            user.name.toLowerCase().includes(searchTerm) ||
            user.email.toLowerCase().includes(searchTerm) ||
            user.phone.includes(searchTerm)
        );
    }
    
    renderUsersList();
}

function showToast(message, type = 'info') {
    // Create toast element
    const toast = document.createElement('div');
    const bgColor = {
        'success': 'bg-success',
        'danger': 'bg-danger',
        'warning': 'bg-warning',
        'info': 'bg-primary'
    }[type] || 'bg-primary';

    toast.className = `fixed top-4 left-4 ${bgColor} text-white px-6 py-3 rounded-lg shadow-lg z-50 fade-in`;
    toast.textContent = message;
    
    document.body.appendChild(toast);
    
    // Remove toast after 3 seconds
    setTimeout(() => {
        toast.remove();
    }, 3000);
}

// Utility Functions
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Export for global use
window.selectUser = selectUser;
window.selectChat = selectChat;