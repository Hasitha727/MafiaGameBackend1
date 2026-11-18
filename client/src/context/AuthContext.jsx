import React, { createContext, useContext, useState, useEffect } from 'react';
import api from '../services/api';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => localStorage.getItem('mafia_jwt_token') || '');
  const [user, setUser] = useState(() => {
    try {
      const raw = localStorage.getItem('mafia_user');
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  });

  // PWA Install Prompt state
  const [deferredPrompt, setDeferredPrompt] = useState(null);
  const [isInstalled, setIsInstalled] = useState(
    () => window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true
  );

  useEffect(() => {
    const handleSessionExpired = () => {
      setToken('');
      setUser(null);
    };

    const handleBeforeInstallPrompt = (e) => {
      e.preventDefault();
      setDeferredPrompt(e);
    };

    const handleAppInstalled = () => {
      setIsInstalled(true);
      setDeferredPrompt(null);
    };

    window.addEventListener('mafia:session-expired', handleSessionExpired);
    window.addEventListener('beforeinstallprompt', handleBeforeInstallPrompt);
    window.addEventListener('appinstalled', handleAppInstalled);

    return () => {
      window.removeEventListener('mafia:session-expired', handleSessionExpired);
      window.removeEventListener('beforeinstallprompt', handleBeforeInstallPrompt);
      window.removeEventListener('appinstalled', handleAppInstalled);
    };
  }, []);

  const persistSession = (data) => {
    const userObj = {
      userId: data.userId,
      username: data.username,
      email: data.email,
      role: data.role || 'Player'
    };
    localStorage.setItem('mafia_jwt_token', data.token);
    localStorage.setItem('mafia_user', JSON.stringify(userObj));
    setToken(data.token);
    setUser(userObj);
  };

  const login = async (usernameOrEmail, password) => {
    const { data } = await api.post('/auth/login', { usernameOrEmail, password });
    persistSession(data);
    return data;
  };

  const register = async (username, email, password) => {
    const { data } = await api.post('/auth/register', { username, email, password });
    persistSession(data);
    return data;
  };

  const logout = () => {
    localStorage.removeItem('mafia_jwt_token');
    localStorage.removeItem('mafia_user');
    setToken('');
    setUser(null);
  };

  const promptInstall = async () => {
    if (!deferredPrompt) return false;
    deferredPrompt.prompt();
    const { outcome } = await deferredPrompt.userChoice;
    if (outcome === 'accepted') {
      setDeferredPrompt(null);
    }
    return outcome === 'accepted';
  };

  return (
    <AuthContext.Provider
      value={{
        token,
        user,
        isAuthenticated: Boolean(token && user),
        isAdmin: user?.role === 'Admin',
        login,
        register,
        logout,
        canInstall: Boolean(deferredPrompt) && !isInstalled,
        isInstalled,
        promptInstall
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
