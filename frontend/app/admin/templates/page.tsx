"use client";

import React, { useEffect, useState } from 'react';
import {
    Typography, Box, Paper, Button, Stack, TextField,
    Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
    Dialog, DialogTitle, DialogContent, DialogActions,
    Chip, CircularProgress, Alert, Snackbar, Input, IconButton
} from '@mui/material';
import {
    CloudUpload as UploadIcon,
    CheckCircle as ActiveIcon,
    History as VersionIcon,
    Delete as DeleteIcon,
    Download as DownloadIcon
} from '@mui/icons-material';
import { templateApi, catalogApi } from '../../services/api';

export default function TemplateManagement() {
    const [templates, setTemplates] = useState<any[]>([]);
    const [regions, setRegions] = useState<any[]>([]);
    const [loading, setLoading] = useState(true);

    // Upload State
    const [uploadOpen, setUploadOpen] = useState(false);
    const [uploadForm, setUploadForm] = useState({ name: '', version: 1, description: '', regionId: '', file: null as File | null });

    const [toast, setToast] = useState({ open: false, message: '', severity: 'success' as any });

    const fetchData = async () => {
        setLoading(true);
        try {
            const [tRes, rRes] = await Promise.all([
                templateApi.list(),
                catalogApi.getRegions()
            ]);
            setTemplates(tRes.data);
            setRegions(rRes.data);
        } catch (err) {
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
    }, []);

    const handleUpload = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!uploadForm.file) return;

        const formData = new FormData();
        formData.append('name', uploadForm.name);
        formData.append('version', uploadForm.version.toString());
        formData.append('description', uploadForm.description);
        formData.append('regionId', uploadForm.regionId);
        formData.append('file', uploadForm.file);

        try {
            await templateApi.create(formData);
            setUploadOpen(false);
            fetchData();
            setToast({ open: true, message: 'Template uploaded successfully', severity: 'success' });
        } catch (err) {
            setToast({ open: true, message: 'Failed to upload template', severity: 'error' });
        }
    };

    const handleActivate = async (id: string) => {
        try {
            await templateApi.activate(id);
            fetchData();
            setToast({ open: true, message: 'Template activated', severity: 'success' });
        } catch (err) {
            setToast({ open: true, message: 'Failed to activate template', severity: 'error' });
        }
    };

    const handleDeactivate = async (id: string) => {
        try {
            await templateApi.deactivate(id);
            fetchData();
            setToast({ open: true, message: 'Template deactivated', severity: 'success' });
        } catch (err) {
            setToast({ open: true, message: 'Failed to deactivate template', severity: 'error' });
        }
    };

    const handleDelete = async (id: string) => {
        if (!window.confirm('Are you sure you want to delete this template?')) return;
        try {
            await templateApi.delete(id);
            fetchData();
            setToast({ open: true, message: 'Template deleted', severity: 'success' });
        } catch (err) {
            setToast({ open: true, message: 'Failed to delete template', severity: 'error' });
        }
    };

    if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>;

    return (
        <Box>
            <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 4 }}>
                <Typography variant="h4" fontWeight="bold">Proposal Templates</Typography>
                <Button variant="contained" startIcon={<UploadIcon />} onClick={() => setUploadOpen(true)}>
                    Upload Template
                </Button>
            </Stack>

            <TableContainer component={Paper}>
                <Table>
                    <TableHead>
                        <TableRow>
                            <TableCell>Name</TableCell>
                            <TableCell>Version</TableCell>
                            <TableCell>Status</TableCell>
                            <TableCell>Uploaded</TableCell>
                            <TableCell align="right">Actions</TableCell>
                        </TableRow>
                    </TableHead>
                    <TableBody>
                        {templates.map((t) => (
                            <TableRow key={t.id}>
                                <TableCell>
                                    <Typography variant="subtitle2">{t.name}</Typography>
                                    <Typography variant="caption" color="text.secondary">{t.description}</Typography>
                                </TableCell>
                                <TableCell>v{t.version}</TableCell>
                                <TableCell>
                                    <Chip
                                        label={t.status}
                                        size="small"
                                        color={t.status === 'Active' ? 'success' : 'default'}
                                        variant={t.status === 'Active' ? 'filled' : 'outlined'}
                                    />
                                </TableCell>
                                <TableCell>{new Date(t.createdAt).toLocaleDateString()}</TableCell>
                                <TableCell align="right">
                                    <Stack direction="row" spacing={1} justifyContent="flex-end">
                                        {t.status === 'Active' ? (
                                            <Button size="small" variant="outlined" color="warning" onClick={() => handleDeactivate(t.id)}>
                                                Deactivate
                                            </Button>
                                        ) : (
                                            <Button size="small" variant="contained" color="success" onClick={() => handleActivate(t.id)}>
                                                Activate
                                            </Button>
                                        )}
                                        <Button size="small" variant="outlined" component="a" href={t.storageKey} target="_blank" startIcon={<DownloadIcon />}>
                                            Download
                                        </Button>
                                        <IconButton size="small" color="error" onClick={() => handleDelete(t.id)}>
                                            <DeleteIcon />
                                        </IconButton>
                                    </Stack>
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </TableContainer>

            {/* Upload Dialog */}
            <Dialog open={uploadOpen} onClose={() => setUploadOpen(false)}>
                <form onSubmit={handleUpload}>
                    <DialogTitle>Upload New PPTX Template</DialogTitle>
                    <DialogContent sx={{ pt: 2 }}>
                        <Stack spacing={3} sx={{ minWidth: 400, mt: 1 }}>
                            <TextField
                                label="Template Name"
                                fullWidth
                                value={uploadForm.name}
                                onChange={(e) => setUploadForm({ ...uploadForm, name: e.target.value })}
                                required
                            />
                            <TextField
                                label="Version Number"
                                type="number"
                                fullWidth
                                value={uploadForm.version}
                                onChange={(e) => setUploadForm({ ...uploadForm, version: parseInt(e.target.value) || 1 })}
                            />
                            <TextField
                                label="Description"
                                fullWidth
                                multiline
                                rows={2}
                                value={uploadForm.description}
                                onChange={(e) => setUploadForm({ ...uploadForm, description: e.target.value })}
                            />
                            <Box sx={{ p: 2, border: '2px dashed #ccc', textAlign: 'center', borderRadius: 1 }}>
                                <input
                                    type="file"
                                    accept=".pptx"
                                    style={{ display: 'none' }}
                                    id="pptx-file"
                                    onChange={(e) => setUploadForm({ ...uploadForm, file: e.target.files?.[0] || null })}
                                />
                                <label htmlFor="pptx-file">
                                    <Button component="span" startIcon={<UploadIcon />}>
                                        {uploadForm.file ? uploadForm.file.name : 'Select PPTX File'}
                                    </Button>
                                </label>
                            </Box>
                        </Stack>
                    </DialogContent>
                    <DialogActions sx={{ p: 3 }}>
                        <Button onClick={() => setUploadOpen(false)}>Cancel</Button>
                        <Button type="submit" variant="contained" disabled={!uploadForm.file}>Upload</Button>
                    </DialogActions>
                </form>
            </Dialog>

            <Snackbar open={toast.open} autoHideDuration={3000} onClose={() => setToast({ ...toast, open: false })}>
                <Alert severity={toast.severity}>{toast.message}</Alert>
            </Snackbar>
        </Box>
    );
}
