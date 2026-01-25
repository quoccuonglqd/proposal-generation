import axios from 'axios';

const api = axios.create({
    baseURL: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5008/api/v1',
    headers: {
        'Content-Type': 'application/json',
    },
});

api.interceptors.request.use((config) => {
    const token = typeof window !== 'undefined' ? localStorage.getItem('token') : null;
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
});

const authApi = {
    login: (data: any) => api.post('/account/login', data),
    getMe: () => api.get('/account/me'),
};

const proposalApi = {
    create: (data: any) => api.post('/proposals', data),
    list: () => api.get('/proposals'),
    getById: (id: string) => api.get(`/proposals/${id}`),
    delete: (id: string) => api.delete(`/proposals/${id}`),
    batchDelete: (ids: string[]) => api.post('/proposals/batch-delete', { proposalIds: ids }),
    updateMetadata: (id: string, data: any) => api.patch(`/proposals/${id}/metadata`, data),
    updateServices: (id: string, items: any[]) => api.put(`/proposals/${id}/services`, { items }),
    updateSectionsOrder: (id: string, orderedKeys: string[]) => api.put(`/proposals/${id}/sections/order`, { orderedKeys }),
    updateSectionContent: (id: string, key: string, data: any) => api.put(`/proposals/${id}/sections/${key}`, data),
    generate: (id: string, data: any) => api.post(`/proposals/${id}/generate`, data),
};

const catalogApi = {
    getRegions: () => api.get('/regions'),
    createRegion: (data: any) => api.post('/regions', data),
    updateRegion: (id: string, data: any) => api.put(`/regions/${id}`, data),
    deleteRegion: (id: string) => api.delete(`/regions/${id}`),
    getServiceTree: (regionId: string) => api.get(`/services/tree?regionId=${regionId}`),
    upsertService: (data: any) => data.id ? api.put(`/services/${data.id}`, data) : api.post('/services', data),
    upsertPrice: (data: any) => api.put('/services/prices', data),
};

const templateApi = {
    list: () => api.get('/templates'),
    create: (formData: FormData) => api.post('/templates', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
    }),
    activate: (id: string) => api.post(`/templates/${id}/activate`),
    deactivate: (id: string) => api.post(`/templates/${id}/deactivate`),
    delete: (id: string) => api.delete(`/templates/${id}`),
};

const adminApi = {
    getExchangeRates: () => api.get('/exchange-rates'),
    updateDefaultRate: (data: any) => api.put('/exchange-rates/default', data),
    getUsers: () => api.get('/users'),
    updateUserRoles: (id: string, roles: string[]) => api.put(`/users/${id}/roles`, { roles }),
};

export { authApi, proposalApi, catalogApi, templateApi, adminApi };
export default api;
