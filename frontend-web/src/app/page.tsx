'use client';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/api/client';
import { useState } from 'react';

export default function AdminDashboard() {
  const queryClient = useQueryClient();
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [newTenant, setNewTenant] = useState({ name: '', slug: '', primaryColor: '#0F172A', secondaryColor: '#3B82F6' });

  const { data: tenantsPage, isLoading } = useQuery({
    queryKey: ['tenants'],
    queryFn: async () => {
      const res = await apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsListTenantsListTenantsEndpoint({
        page: 1,
        perPage: 100
      });
      return res.data;
    }
  });

  const createTenantMutation = useMutation({
    mutationFn: async (tenantData: any) => {
      const res = await apiClient.tenants.alphaZeroModulesTenantsPresentationEndpointsCreateTenantCreateTenantEndpoint(tenantData);
      return res.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['tenants'] });
      setIsModalOpen(false);
      setNewTenant({ name: '', slug: '', primaryColor: '#0F172A', secondaryColor: '#3B82F6' });
    }
  });

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    createTenantMutation.mutate({
      name: newTenant.name,
      subdomain: newTenant.slug,
      primaryColor: newTenant.primaryColor,
      secondaryColor: newTenant.secondaryColor,
      logoUrl: '' 
    });
  };

  return (
    <div className="min-h-screen bg-[var(--bg-color)] text-[var(--text-primary)] font-sans" dir="ltr">
      {/* Brutalist Header */}
      <header className="border-b-[3px] border-[var(--text-primary)] px-8 py-10 lg:py-16">
        <div className="max-w-7xl mx-auto flex flex-col md:flex-row justify-between items-start md:items-end gap-6">
          <div>
            <span className="font-mono text-sm tracking-widest uppercase mb-4 block opacity-70">AlphaZero Infrastructure</span>
            <h1 className="text-5xl md:text-7xl font-black tracking-tighter leading-none">
              TENANT<br />COMMAND
            </h1>
          </div>
          <button 
            onClick={() => setIsModalOpen(true)}
            className="group relative inline-flex items-center justify-center bg-[var(--text-primary)] text-[var(--bg-color)] px-8 py-4 font-bold uppercase tracking-wider text-sm transition-transform hover:-translate-y-1 hover:translate-x-1"
          >
            <span className="absolute inset-0 border-2 border-[var(--text-primary)] -translate-x-2 translate-y-2 -z-10 group-hover:translate-x-0 group-hover:translate-y-0 transition-transform"></span>
            Provision Academy
          </button>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-8 py-16">
        {/* Metric Section - Asymmetric Grid */}
        <div className="grid grid-cols-1 md:grid-cols-12 gap-8 mb-24">
          <div className="md:col-span-5 flex flex-col justify-end">
            <h2 className="text-3xl font-bold tracking-tight mb-4">System Status</h2>
            <p className="text-[var(--text-muted)] text-lg max-w-md">
              Monitoring global multi-tenant performance and resource allocations across all active learning academies.
            </p>
          </div>
          <div className="md:col-span-7 grid grid-cols-2 gap-4">
            <div className="border-[3px] border-[var(--text-primary)] p-6 bg-[var(--surface-color)]">
              <span className="font-mono text-xs uppercase tracking-widest block mb-8">Active Nodes</span>
              <p className="text-6xl font-black">{tenantsPage?.items?.length || 0}</p>
            </div>
            <div className="border-[3px] border-[var(--text-primary)] p-6 bg-[var(--surface-color)]">
              <span className="font-mono text-xs uppercase tracking-widest block mb-8">Global Uptime</span>
              <p className="text-6xl font-black">99.9<span className="text-3xl">%</span></p>
            </div>
          </div>
        </div>

        {/* Datagrid Section */}
        <section>
          <div className="flex items-center justify-between mb-8">
            <h3 className="text-2xl font-bold">Network Topology</h3>
            <span className="font-mono text-xs border border-[var(--text-primary)] px-2 py-1">LIVE FEED</span>
          </div>

          {isLoading ? (
            <div className="border-[3px] border-[var(--text-primary)] h-64 flex items-center justify-center font-mono animate-pulse">
              [ INITIALIZING TELEMETRY ]
            </div>
          ) : (
            <div className="border-[3px] border-[var(--text-primary)] overflow-x-auto bg-[var(--surface-color)]">
              <table className="w-full text-left font-mono text-sm whitespace-nowrap">
                <thead className="bg-[var(--text-primary)] text-[var(--bg-color)]">
                  <tr>
                    <th className="p-4 font-bold tracking-wider">ACADEMY_ID</th>
                    <th className="p-4 font-bold tracking-wider">ROUTING_SLUG</th>
                    <th className="p-4 font-bold tracking-wider">THEME_VECTOR</th>
                    <th className="p-4 font-bold tracking-wider text-right">OPERATIONS</th>
                  </tr>
                </thead>
                <tbody className="divide-y-[3px] divide-[var(--text-primary)]">
                  {tenantsPage?.items?.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="p-12 text-center text-[var(--text-muted)] uppercase">No active nodes detected. Provision required.</td>
                    </tr>
                  ) : (
                    tenantsPage?.items?.map((tenant: any) => (
                      <tr key={tenant.id} className="hover:bg-[var(--text-primary)] hover:text-[var(--bg-color)] transition-colors group">
                        <td className="p-4 font-bold">{tenant.name}</td>
                        <td className="p-4">
                          <a href={`http://${tenant.subdomain}.localhost:3000`} className="underline decoration-2 underline-offset-4" target="_blank" rel="noreferrer">
                            {tenant.subdomain}.localhost:3000
                          </a>
                        </td>
                        <td className="p-4">
                          <div className="flex items-center gap-3">
                            <div className="w-5 h-5 border-2 border-current" style={{ backgroundColor: tenant.primaryColor || '#000' }}></div>
                            <span>{tenant.primaryColor || 'DEFAULT'}</span>
                          </div>
                        </td>
                        <td className="p-4 text-right">
                          <button className="uppercase font-bold tracking-widest text-xs border-b-2 border-transparent group-hover:border-[var(--bg-color)] transition-colors">
                            Configure
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </main>

      {/* Brutalist Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-[var(--bg-color)]/90 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="border-[4px] border-[var(--text-primary)] bg-[var(--surface-color)] w-full max-w-2xl shadow-[16px_16px_0px_0px_var(--text-primary)]">
            <div className="border-b-[4px] border-[var(--text-primary)] p-6 bg-[var(--text-primary)] text-[var(--bg-color)] flex justify-between items-center">
              <h2 className="text-2xl font-black uppercase tracking-widest">Provision Node</h2>
              <button onClick={() => setIsModalOpen(false)} className="text-xl font-bold hover:opacity-70">✕</button>
            </div>
            
            <form onSubmit={handleCreate} className="p-8 space-y-8">
              <div className="space-y-6">
                <div>
                  <label className="block font-mono text-sm font-bold mb-2 uppercase">Entity Name</label>
                  <input 
                    type="text" 
                    required
                    value={newTenant.name}
                    onChange={(e) => setNewTenant({...newTenant, name: e.target.value})}
                    className="w-full border-[3px] border-[var(--text-primary)] bg-transparent p-4 outline-none focus:bg-[var(--text-primary)] focus:text-[var(--bg-color)] transition-colors font-mono" 
                    placeholder="e.g. Damascus Institute"
                  />
                </div>
                <div>
                  <label className="block font-mono text-sm font-bold mb-2 uppercase">Routing Slug (Subdomain)</label>
                  <input 
                    type="text" 
                    required
                    value={newTenant.slug}
                    onChange={(e) => setNewTenant({...newTenant, slug: e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '')})}
                    className="w-full border-[3px] border-[var(--text-primary)] bg-transparent p-4 outline-none focus:bg-[var(--text-primary)] focus:text-[var(--bg-color)] transition-colors font-mono" 
                    placeholder="e.g. damascus"
                  />
                </div>
                <div className="grid grid-cols-2 gap-6">
                  <div>
                    <label className="block font-mono text-sm font-bold mb-2 uppercase">Primary Vector</label>
                    <div className="flex items-center gap-4">
                      <input 
                        type="color" 
                        value={newTenant.primaryColor}
                        onChange={(e) => setNewTenant({...newTenant, primaryColor: e.target.value})}
                        className="w-14 h-14 border-[3px] border-[var(--text-primary)] p-0 cursor-pointer" 
                      />
                      <span className="font-mono uppercase">{newTenant.primaryColor}</span>
                    </div>
                  </div>
                  <div>
                    <label className="block font-mono text-sm font-bold mb-2 uppercase">Secondary Vector</label>
                    <div className="flex items-center gap-4">
                      <input 
                        type="color" 
                        value={newTenant.secondaryColor}
                        onChange={(e) => setNewTenant({...newTenant, secondaryColor: e.target.value})}
                        className="w-14 h-14 border-[3px] border-[var(--text-primary)] p-0 cursor-pointer" 
                      />
                      <span className="font-mono uppercase">{newTenant.secondaryColor}</span>
                    </div>
                  </div>
                </div>
              </div>
              
              <div className="pt-8 border-t-[3px] border-[var(--text-primary)] flex justify-end gap-4">
                <button 
                  type="button" 
                  onClick={() => setIsModalOpen(false)}
                  className="px-8 py-4 font-bold uppercase tracking-wider text-sm border-[3px] border-[var(--text-primary)] hover:bg-[var(--text-primary)] hover:text-[var(--bg-color)] transition-colors"
                >
                  Abort
                </button>
                <button 
                  type="submit" 
                  disabled={createTenantMutation.isPending}
                  className="bg-[var(--text-primary)] text-[var(--bg-color)] px-8 py-4 font-bold uppercase tracking-wider text-sm hover:opacity-90 disabled:opacity-50 transition-opacity"
                >
                  {createTenantMutation.isPending ? 'Executing...' : 'Execute Provision'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
