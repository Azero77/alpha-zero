import { DarkTheme, DefaultTheme, ThemeProvider } from 'expo-router';
import { useColorScheme, I18nManager } from 'react-native';

// Enforce RTL Layout for Arabic Localization (T2)
I18nManager.allowRTL(true);
I18nManager.forceRTL(true);

import { AnimatedSplashOverlay } from '@/components/animated-icon';
import AppTabs from '@/components/app-tabs';

export default function TabLayout() {
  const colorScheme = useColorScheme();
  return (
    <ThemeProvider value={colorScheme === 'dark' ? DarkTheme : DefaultTheme}>
      <AnimatedSplashOverlay />
      <AppTabs />
    </ThemeProvider>
  );
}
