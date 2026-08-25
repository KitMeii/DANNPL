import React, { createContext, useContext, useState, useEffect } from 'react';
import { authApi } from '../services/api';

const AuthContext = createContext(null);

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Check for existing session
    const token = localStorage.getItem('token');
    const savedUser = localStorage.getItem('user');
    if (token && savedUser) {
      try {
        setUser(JSON.parse(savedUser));
      } catch (e) {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
      }
    }
    setLoading(false);
  }, []);

  const login = async (userName, password) => {
    try {
      console.log('AuthContext: Calling login API...');
      const response = await authApi.login(userName, password);
      console.log('AuthContext: API response:', response.data);

      const { token, userId, username, name, email, role } = response.data;

      if (!token) {
        console.error('AuthContext: No token in response');
        return { success: false, error: 'No token received from server' };
      }

      localStorage.setItem('token', token);
      const userData = { userId, username, name, email, role };
      localStorage.setItem('user', JSON.stringify(userData));
      setUser(userData);

      console.log('AuthContext: Login successful, user set:', userData);
      return { success: true };
    } catch (error) {
      console.error('AuthContext: Login error:', error);
      const message = error.response?.data?.message || error.response?.data || 'Login failed';
      return { success: false, error: message };
    }
  };

  const logout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    setUser(null);
  };

  const value = {
    user,
    loading,
    login,
    logout,
    isAuthenticated: !!user,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
};

export default AuthContext;
