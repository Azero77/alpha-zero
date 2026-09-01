import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { queryConfig } from "../lib/react-query";
import React from "react";
import { HelmetProvider } from "react-helmet-async";
import { MainErrorFallback } from "../components/errors/main";
import { ErrorBoundary } from "react-error-boundary";

type AppProviderProps = {
    children : React.ReactNode
}

export const AppProvider = ({children} : AppProviderProps) => {
    const [queryClient] = React.useState(
        () => 
            new QueryClient(
                {
                    defaultOptions : queryConfig
                }
            )
    );

    return (
        <>
            <React.Suspense
                fallback={
                    <div className="flex justify-center items-center h-screen">
                        <div className="text-2xl font-bold">Loading...</div>
                    </div>
                }
            >
                <ErrorBoundary FallbackComponent = {MainErrorFallback}>
                    <HelmetProvider>
                        <QueryClientProvider client = {queryClient}>
                            <>
                                {children}
                            </>
                        </QueryClientProvider>
                    </HelmetProvider>
                </ErrorBoundary>
            </React.Suspense>
        </>
    )
} 

