"use client";

import React, { useEffect, useState } from 'react';
import {
    Typography, Box, Paper, Button, Stack, TextField,
    IconButton, List, ListItem, ListItemText, ListItemSecondaryAction,
    Dialog, DialogTitle, DialogContent, DialogActions,
    CircularProgress, Alert, Snackbar
} from '@mui/material';
import {
    Add as AddIcon,
    Edit as EditIcon,
    Delete as DeleteIcon,
    CloudUpload as CloudUploadIcon
} from '@mui/icons-material';
import { masterSectionsApi, assetsApi } from '../../services/api';

export default function MasterSectionsPage() {
    const [sections, setSections] = useState<any[]>([]);
    const [loading, setLoading] = useState(true);
    const [editorOpen, setEditorOpen] = useState(false);
    const [editingSection, setEditingSection] = useState<any>(null);
    const [toast, setToast] = useState({ open: false, message: '', severity: 'success' as any });

    const fetchSections = async () => {
        setLoading(true);
        try {
            const res = await masterSectionsApi.list();
            setSections(res.data);
        } catch (err) {
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    const handleImageUpload = async (file: File, lang: 'en' | 'vn', slideIndex: number) => {
        try {
            const res = await assetsApi.upload(file);
            const assetId = res.data.assetId;
            const newSlides = [...(editingSection?.defaultContent?.[lang] || [])];
            newSlides[slideIndex] = { ...newSlides[slideIndex], backgroundAssetId: assetId };
            setEditingSection({
                ...editingSection,
                defaultContent: { ...editingSection.defaultContent, [lang]: newSlides }
            });
            setToast({ open: true, message: 'Image uploaded successfully', severity: 'success' });
        } catch (err) {
            console.error(err);
            setToast({ open: true, message: 'Failed to upload image', severity: 'error' });
        }
    };

    useEffect(() => {
        fetchSections();
    }, []);

    const handleSave = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            if (editingSection.id) {
                await masterSectionsApi.update(editingSection.id, {
                    sectionKey: editingSection.sectionKey,
                    name: editingSection.name,
                    defaultContent: editingSection.defaultContent,
                    sortOrder: editingSection.sortOrder,
                    isActive: editingSection.isActive
                });
            } else {
                await masterSectionsApi.create({
                    sectionKey: editingSection.sectionKey,
                    name: editingSection.name,
                    defaultContent: editingSection.defaultContent,
                    sortOrder: editingSection.sortOrder,
                    isActive: editingSection.isActive
                });
            }
            setEditorOpen(false);
            fetchSections();
            setToast({ open: true, message: 'Section saved', severity: 'success' });
        } catch (err) {
            setToast({ open: true, message: 'Failed to save section', severity: 'error' });
        }
    };

    const handleDelete = async (id: string) => {
        if (!confirm('Are you sure you want to delete this section?')) return;
        try {
            await masterSectionsApi.delete(id);
            fetchSections();
            setToast({ open: true, message: 'Section deleted', severity: 'success' });
        } catch (err) {
            setToast({ open: true, message: 'Failed to delete section', severity: 'error' });
        }
    };

    return (
        <Box>
            <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 4 }}>
                <Typography variant="h4" fontWeight="bold">Proposal Sections</Typography>
                <Button variant="contained" startIcon={<AddIcon />} onClick={() => {
                    setEditingSection({
                        sectionKey: '',
                        name: '',
                        sortOrder: sections.length + 1,
                        isActive: true,
                        defaultContent: { en: [], vn: [] }
                    });
                    setEditorOpen(true);
                }}>
                    Add Section
                </Button>
            </Stack>

            <Paper>
                {loading ? (
                    <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>
                ) : (
                    <List disablePadding>
                        {sections.map(s => (
                            <ListItem
                                key={s.id}
                                sx={{
                                    borderBottom: '1px solid #f0f0f0',
                                    '&:hover': { backgroundColor: '#fafafa' }
                                }}
                            >
                                <ListItemText
                                    primary={s.name}
                                    secondary={`Key: ${s.sectionKey} | Order: ${s.sortOrder}`}
                                    primaryTypographyProps={{ fontWeight: 'medium' }}
                                />
                                <ListItemSecondaryAction>
                                    <Stack direction="row" spacing={1}>
                                        <IconButton size="small" onClick={() => {
                                            setEditingSection(s);
                                            setEditorOpen(true);
                                        }}>
                                            <EditIcon fontSize="small" />
                                        </IconButton>
                                        <IconButton size="small" onClick={() => handleDelete(s.id)}>
                                            <DeleteIcon fontSize="small" />
                                        </IconButton>
                                    </Stack>
                                </ListItemSecondaryAction>
                            </ListItem>
                        ))}
                        {sections.length === 0 && <Typography align="center" sx={{ py: 4 }}>No sections found.</Typography>}
                    </List>
                )}
            </Paper>

            {/* Section Editor Dialog */}
            <Dialog open={editorOpen} onClose={() => setEditorOpen(false)} maxWidth="md" fullWidth>
                <form onSubmit={handleSave}>
                    <DialogTitle>{editingSection?.id ? 'Edit Section' : 'Add New Section'}</DialogTitle>
                    <DialogContent sx={{ pt: 2 }}>
                        <Stack spacing={3} sx={{ mt: 1 }}>
                            <TextField
                                label="Section Key"
                                fullWidth
                                value={editingSection?.sectionKey || ''}
                                onChange={(e) => setEditingSection({ ...editingSection, sectionKey: e.target.value })}
                                required
                                helperText="e.g., Executive_Summary"
                            />
                            <TextField
                                label="Section Name"
                                fullWidth
                                value={editingSection?.name || ''}
                                onChange={(e) => setEditingSection({ ...editingSection, name: e.target.value })}
                                required
                            />
                            <TextField
                                label="Sort Order"
                                type="number"
                                fullWidth
                                value={editingSection?.sortOrder || 0}
                                onChange={(e) => setEditingSection({ ...editingSection, sortOrder: parseInt(e.target.value) })}
                            />

                            {/* English Slides Editor */}
                            <Typography variant="subtitle1" fontWeight="bold">Slides (English)</Typography>
                            <Box sx={{ border: '1px solid #e0e0e0', borderRadius: 1, p: 2 }}>
                                {(Array.isArray(editingSection?.defaultContent?.en) ? editingSection?.defaultContent?.en : []).map((slide: any, index: number) => (
                                    <Box key={index} sx={{ mb: 2, p: 2, bgcolor: '#f9f9f9', borderRadius: 1, position: 'relative' }}>
                                        <IconButton
                                            size="small"
                                            onClick={() => {
                                                const newSlides = [...(editingSection?.defaultContent?.en || [])];
                                                newSlides.splice(index, 1);
                                                setEditingSection({
                                                    ...editingSection,
                                                    defaultContent: { ...editingSection.defaultContent, en: newSlides }
                                                });
                                            }}
                                            sx={{ position: 'absolute', right: 8, top: 8 }}
                                        >
                                            <DeleteIcon fontSize="small" />
                                        </IconButton>
                                        <Typography variant="caption" sx={{ mb: 1, display: 'block' }}>Slide {index + 1}</Typography>
                                        <Stack spacing={2}>
                                            <TextField
                                                label="Slide Title"
                                                multiline
                                                rows={2}
                                                size="small"
                                                fullWidth
                                                value={slide.title || ''}
                                                onChange={(e) => {
                                                    const newSlides = [...(editingSection?.defaultContent?.en || [])];
                                                    newSlides[index] = { ...slide, title: e.target.value };
                                                    setEditingSection({
                                                        ...editingSection,
                                                        defaultContent: { ...editingSection.defaultContent, en: newSlides }
                                                    });
                                                }}
                                            />
                                            <TextField
                                                label="Slide Content"
                                                multiline
                                                rows={3}
                                                size="small"
                                                fullWidth
                                                value={slide.content || ''}
                                                onChange={(e) => {
                                                    const newSlides = [...(editingSection?.defaultContent?.en || [])];
                                                    newSlides[index] = { ...slide, content: e.target.value };
                                                    setEditingSection({
                                                        ...editingSection,
                                                        defaultContent: { ...editingSection.defaultContent, en: newSlides }
                                                    });
                                                }}
                                            />
                                            <Box>
                                                <Typography variant="caption" color="textSecondary" sx={{ mb: 1, display: 'block' }}>Slide Background</Typography>
                                                {slide.backgroundAssetId ? (
                                                    <Box sx={{ position: 'relative', width: 'fit-content' }}>
                                                        <img
                                                            src={assetsApi.getDownloadUrl(slide.backgroundAssetId)}
                                                            alt="Background"
                                                            style={{ height: 80, borderRadius: 4, display: 'block', border: '1px solid #ddd' }}
                                                        />
                                                        <IconButton
                                                            size="small"
                                                            color="error"
                                                            sx={{
                                                                position: 'absolute',
                                                                top: -8,
                                                                right: -8,
                                                                bgcolor: 'white',
                                                                boxShadow: 1,
                                                                '&:hover': { bgcolor: '#f5f5f5' }
                                                            }}
                                                            onClick={() => {
                                                                const newSlides = [...(editingSection?.defaultContent?.en || [])];
                                                                newSlides[index] = { ...slide, backgroundAssetId: '' };
                                                                setEditingSection({
                                                                    ...editingSection,
                                                                    defaultContent: { ...editingSection.defaultContent, en: newSlides }
                                                                });
                                                            }}
                                                        >
                                                            <DeleteIcon fontSize="inherit" />
                                                        </IconButton>
                                                    </Box>
                                                ) : (
                                                    <Button
                                                        variant="outlined"
                                                        size="small"
                                                        component="label"
                                                        startIcon={<CloudUploadIcon />}
                                                        sx={{ textTransform: 'none' }}
                                                    >
                                                        Upload Background
                                                        <input
                                                            type="file"
                                                            hidden
                                                            accept="image/*"
                                                            onChange={(e) => {
                                                                const file = e.target.files?.[0];
                                                                if (file) handleImageUpload(file, 'en', index);
                                                            }}
                                                        />
                                                    </Button>
                                                )}
                                            </Box>
                                        </Stack>
                                    </Box>
                                ))}
                                <Button
                                    startIcon={<AddIcon />}
                                    size="small"
                                    onClick={() => {
                                        const currentSlides = Array.isArray(editingSection?.defaultContent?.en) ? editingSection?.defaultContent?.en : [];
                                        setEditingSection({
                                            ...editingSection,
                                            defaultContent: { ...editingSection.defaultContent, en: [...currentSlides, { title: '', content: '', backgroundAssetId: '' }] }
                                        });
                                    }}
                                >
                                    Add Slide
                                </Button>
                            </Box>

                            {/* Vietnamese Slides Editor */}
                            <Typography variant="subtitle1" fontWeight="bold">Slides (Vietnamese)</Typography>
                            <Box sx={{ border: '1px solid #e0e0e0', borderRadius: 1, p: 2 }}>
                                {(Array.isArray(editingSection?.defaultContent?.vn) ? editingSection?.defaultContent?.vn : []).map((slide: any, index: number) => (
                                    <Box key={index} sx={{ mb: 2, p: 2, bgcolor: '#f9f9f9', borderRadius: 1, position: 'relative' }}>
                                        <IconButton
                                            size="small"
                                            onClick={() => {
                                                const newSlides = [...(editingSection?.defaultContent?.vn || [])];
                                                newSlides.splice(index, 1);
                                                setEditingSection({
                                                    ...editingSection,
                                                    defaultContent: { ...editingSection.defaultContent, vn: newSlides }
                                                });
                                            }}
                                            sx={{ position: 'absolute', right: 8, top: 8 }}
                                        >
                                            <DeleteIcon fontSize="small" />
                                        </IconButton>
                                        <Typography variant="caption" sx={{ mb: 1, display: 'block' }}>Slide {index + 1}</Typography>
                                        <Stack spacing={2}>
                                            <TextField
                                                label="Slide Title"
                                                multiline
                                                rows={2}
                                                size="small"
                                                fullWidth
                                                value={slide.title || ''}
                                                onChange={(e) => {
                                                    const newSlides = [...(editingSection?.defaultContent?.vn || [])];
                                                    newSlides[index] = { ...slide, title: e.target.value };
                                                    setEditingSection({
                                                        ...editingSection,
                                                        defaultContent: { ...editingSection.defaultContent, vn: newSlides }
                                                    });
                                                }}
                                            />
                                            <TextField
                                                label="Slide Content"
                                                multiline
                                                rows={3}
                                                size="small"
                                                fullWidth
                                                value={slide.content || ''}
                                                onChange={(e) => {
                                                    const newSlides = [...(editingSection?.defaultContent?.vn || [])];
                                                    newSlides[index] = { ...slide, content: e.target.value };
                                                    setEditingSection({
                                                        ...editingSection,
                                                        defaultContent: { ...editingSection.defaultContent, vn: newSlides }
                                                    });
                                                }}
                                            />
                                            <Box>
                                                <Typography variant="caption" color="textSecondary" sx={{ mb: 1, display: 'block' }}>Slide Background</Typography>
                                                {slide.backgroundAssetId ? (
                                                    <Box sx={{ position: 'relative', width: 'fit-content' }}>
                                                        <img
                                                            src={assetsApi.getDownloadUrl(slide.backgroundAssetId)}
                                                            alt="Background"
                                                            style={{ height: 80, borderRadius: 4, display: 'block', border: '1px solid #ddd' }}
                                                        />
                                                        <IconButton
                                                            size="small"
                                                            color="error"
                                                            sx={{
                                                                position: 'absolute',
                                                                top: -8,
                                                                right: -8,
                                                                bgcolor: 'white',
                                                                boxShadow: 1,
                                                                '&:hover': { bgcolor: '#f5f5f5' }
                                                            }}
                                                            onClick={() => {
                                                                const newSlides = [...(editingSection?.defaultContent?.vn || [])];
                                                                newSlides[index] = { ...slide, backgroundAssetId: '' };
                                                                setEditingSection({
                                                                    ...editingSection,
                                                                    defaultContent: { ...editingSection.defaultContent, vn: newSlides }
                                                                });
                                                            }}
                                                        >
                                                            <DeleteIcon fontSize="inherit" />
                                                        </IconButton>
                                                    </Box>
                                                ) : (
                                                    <Button
                                                        variant="outlined"
                                                        size="small"
                                                        component="label"
                                                        startIcon={<CloudUploadIcon />}
                                                        sx={{ textTransform: 'none' }}
                                                    >
                                                        Upload Background
                                                        <input
                                                            type="file"
                                                            hidden
                                                            accept="image/*"
                                                            onChange={(e) => {
                                                                const file = e.target.files?.[0];
                                                                if (file) handleImageUpload(file, 'vn', index);
                                                            }}
                                                        />
                                                    </Button>
                                                )}
                                            </Box>
                                        </Stack>
                                    </Box>
                                ))}
                                <Button
                                    startIcon={<AddIcon />}
                                    size="small"
                                    onClick={() => {
                                        const currentSlides = Array.isArray(editingSection?.defaultContent?.vn) ? editingSection?.defaultContent?.vn : [];
                                        setEditingSection({
                                            ...editingSection,
                                            defaultContent: { ...editingSection.defaultContent, vn: [...currentSlides, { title: '', content: '', backgroundAssetId: '' }] }
                                        });
                                    }}
                                >
                                    Add Slide
                                </Button>
                            </Box>
                        </Stack>
                    </DialogContent>
                    <DialogActions sx={{ p: 3 }}>
                        <Button onClick={() => setEditorOpen(false)}>Cancel</Button>
                        <Button type="submit" variant="contained">Save Changes</Button>
                    </DialogActions>
                </form>
            </Dialog>

            <Snackbar open={toast.open} autoHideDuration={3000} onClose={() => setToast({ ...toast, open: false })}>
                <Alert severity={toast.severity}>{toast.message}</Alert>
            </Snackbar>
        </Box>
    );
}
