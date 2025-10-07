import React from 'react';

class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true };
  }

  componentDidCatch(error, errorInfo) {
    console.error('Error caught by boundary:', error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="text-center p-4 bg-red-50 rounded-lg">
          <h3 className="text-red-600 font-medium mb-2">خطایی رخ داد</h3>
          <p className="text-gray-600">متاسفانه در نمایش این بخش مشکلی پیش آمده است</p>
          <button
            onClick={() => this.setState({ hasError: false })}
            className="mt-2 px-4 py-2 bg-red-100 text-red-700 rounded-lg hover:bg-red-200 transition"
          >
            تلاش مجدد
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}

export default ErrorBoundary;
