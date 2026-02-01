"use client";

import React, { useEffect, useState } from 'react';
import {
    Typography, Box, Paper, Button, Stack, TextField,
    IconButton, List, ListItem, ListItemText, ListItemSecondaryAction,
    Collapse, Dialog, DialogTitle, DialogContent, DialogActions,
    MenuItem, Select, FormControl, InputLabel, Divider,
    Drawer, CircularProgress, Alert, Snackbar
} from '@mui/material';
import {
    Add as AddIcon,
    Edit as EditIcon,
    Delete as DeleteIcon,
    ExpandMore,
    ExpandLess,
    AttachMoney as PriceIcon
} from '@mui/icons-material';
import { catalogApi } from '../../services/api';

export default function ServicesPricing() {
    const [regions, setRegions] = useState<any[]>([]);
    const [selectedRegionId, setSelectedRegionId] = useState('');
    const [serviceTree, setServiceTree] = useState<any[]>([]);
    const [loading, setLoading] = useState(true);
    const [expanded, setExpanded] = useState<string[]>([]);

    // Service Editor State
    const [editorOpen, setEditorOpen] = useState(false);
    const [editingService, setEditingService] = useState<any>(null);

    // Pricing Drawer State
    const [pricingOpen, setPricingOpen] = useState(false);
    const [pricingService, setPricingService] = useState<any>(null);
    const [pricingForm, setPricingForm] = useState({ local: 0, usdRef: 0 });

    const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);
    const [toast, setToast] = useState({ open: false, message: '', severity: 'success' as any });

    useEffect(() => {
        catalogApi.getRegions().then((res: any) => {
            setRegions(res.data);
            if (res.data.length > 0) setSelectedRegionId(res.data[0].id);
        });
    }, []);

    const fetchTree = async (regionId: string) => {
        setLoading(true);
        try {
            const res = await catalogApi.getServiceTree(regionId);
            setServiceTree(res.data.items);
        } catch (err) {
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        if (selectedRegionId) fetchTree(selectedRegionId);
    }, [selectedRegionId]);

    const handleSaveService = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await catalogApi.upsertService(editingService);
            setEditorOpen(false);
            fetchTree(selectedRegionId);
            setToast({ open: true, message: 'Service saved', severity: 'success' });
        } catch (err) {
            setToast({ open: true, message: 'Failed to save service', severity: 'error' });
        }
    };

    const handleSavePrice = async () => {
        try {
            await catalogApi.upsertPrice({
                serviceId: pricingService.id,
                regionId: selectedRegionId,
                localPrice: pricingForm.local,
                usdReferencePrice: pricingForm.usdRef,
                effectiveFrom: new Date().toISOString()
            });
            setPricingOpen(false);
            fetchTree(selectedRegionId);
            setToast({ open: true, message: 'Price updated', severity: 'success' });
        } catch (err) {
            setToast({ open: true, message: 'Failed to update price', severity: 'error' });
        }
    };

    const toggleExpand = (id: string) => {
        setExpanded(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]);
    };

    const handleDelete = async () => {
        if (!deleteConfirm) return;
        try {
            await catalogApi.deleteService(deleteConfirm);
            setDeleteConfirm(null);
            fetchTree(selectedRegionId);
            setToast({ open: true, message: 'Service deleted', severity: 'success' });
        } catch (err) {
            setToast({ open: true, message: 'Failed to delete service', severity: 'error' });
        }
    };

    const renderService = (s: any, depth = 0) => (
        <React.Fragment key={s.id}>
            <ListItem
                sx={{
                    pl: depth * 4 + 2,
                    borderBottom: '1px solid #f0f0f0',
                    '&:hover': { backgroundColor: '#fafafa' }
                }}
            >
                <ListItemText
                    primary={s.name}
                    secondary={
                        <Stack component="span">
                            <Typography component="span" variant="caption" color="text.secondary">
                                {s.level} {s.unit ? `• ${s.unit}` : ''}
                            </Typography>
                        </Stack>
                    }
                    primaryTypographyProps={{ fontWeight: 'medium' }}
                />
                <ListItemSecondaryAction>
                    <Stack direction="row" spacing={1}>
                        <Typography variant="body2" color="primary" sx={{ my: 'auto', mr: 2 }}>
                            {s.price.local.toLocaleString()} {regions.find(r => r.id === selectedRegionId)?.localCurrency}
                        </Typography>
                        {/* Conditional Pricing: Disable if has children */}
                        <IconButton
                            size="small"
                            onClick={() => {
                                setPricingService(s);
                                setPricingForm({ local: s.price.local, usdRef: s.price.usdRef });
                                setPricingOpen(true);
                            }}
                            disabled={s.children?.length > 0}
                            title={s.children?.length > 0 ? "Cannot price parent services" : "Manage Price"}
                        >
                            <PriceIcon fontSize="small" color={s.children?.length > 0 ? "disabled" : "primary"} />
                        </IconButton>

                        {/* Add Child Button: Only for Main and Sub levels */}
                        {(s.level === 'MAIN' || s.level === 'SUB') && (
                            <IconButton
                                size="small"
                                color="success"
                                onClick={() => {
                                    setEditingService({
                                        name: '',
                                        level: s.level === 'MAIN' ? 'sub' : 'lineitem',
                                        parentId: s.id,
                                        sortOrder: s.children?.length || 0,
                                        isActive: true
                                    });
                                    setEditorOpen(true);
                                }}
                                title="Add Child Service"
                            >
                                <AddIcon fontSize="small" />
                            </IconButton>
                        )}

                        <IconButton size="small" onClick={() => {
                            setEditingService({ ...s, level: s.level.toLowerCase() });
                            setEditorOpen(true);
                        }}>
                            <EditIcon fontSize="small" />
                        </IconButton>

                        <IconButton size="small" color="error" onClick={() => setDeleteConfirm(s.id)}>
                            <DeleteIcon fontSize="small" />
                        </IconButton>

                        {s.children?.length > 0 ? (
                            <IconButton size="small" onClick={() => toggleExpand(s.id)}>
                                {expanded.includes(s.id) ? <ExpandLess /> : <ExpandMore />}
                            </IconButton>
                        ) : (
                            <IconButton size="small" disabled sx={{ visibility: 'hidden' }}><ExpandMore /></IconButton>
                        )}
                    </Stack>
                </ListItemSecondaryAction>
            </ListItem>
            {s.children?.length > 0 && (
                <Collapse in={expanded.includes(s.id)} timeout="auto" unmountOnExit>
                    <List component="div" disablePadding>
                        {s.children.map((child: any) => renderService(child, depth + 1))}
                    </List>
                </Collapse>
            )}
        </React.Fragment >
    );

    return (
        <Box>
            <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 4 }}>
                <Typography variant="h4" fontWeight="bold">Services & Pricing</Typography>
                <Stack direction="row" spacing={2} alignItems="center">
                    <FormControl size="small" sx={{ minWidth: 200 }}>
                        <InputLabel>View Pricing For</InputLabel>
                        <Select
                            value={selectedRegionId}
                            label="View Pricing For"
                            onChange={(e) => setSelectedRegionId(e.target.value)}
                        >
                            {regions.map(r => <MenuItem key={r.id} value={r.id}>{r.name} ({r.code})</MenuItem>)}
                        </Select>
                    </FormControl>
                    <Button variant="contained" startIcon={<AddIcon />} onClick={() => {
                        setEditingService({ name: '', level: 'main', sortOrder: 0, isActive: true });
                        setEditorOpen(true);
                    }}>
                        Add Service
                    </Button>
                </Stack>
            </Stack>

            <Paper>
                {loading ? (
                    <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>
                ) : (
                    <List disablePadding>
                        {serviceTree.map(s => renderService(s))}
                        {serviceTree.length === 0 && <Typography align="center" sx={{ py: 4 }}>No services found.</Typography>}
                    </List>
                )}
            </Paper>

            {/* Service Editor Dialog */}
            <Dialog open={editorOpen} onClose={() => setEditorOpen(false)}>
                <form onSubmit={handleSaveService}>
                    <DialogTitle>{editingService?.id ? 'Edit Service' : 'Add New Service'}</DialogTitle>
                    <DialogContent sx={{ pt: 2 }}>
                        <Stack spacing={3} sx={{ minWidth: 400, mt: 1 }}>
                            <TextField
                                label="Service Name"
                                fullWidth
                                value={editingService?.name || ''}
                                onChange={(e) => setEditingService({ ...editingService, name: e.target.value })}
                                required
                            />
                            <TextField
                                label="Unit"
                                placeholder="e.g. Month, Person, Project"
                                fullWidth
                                value={editingService?.unit || ''}
                                onChange={(e) => setEditingService({ ...editingService, unit: e.target.value })}
                            />
                            <FormControl fullWidth>
                                <InputLabel>Level</InputLabel>
                                <Select
                                    label="Level"
                                    value={editingService?.level || 'main'}
                                    onChange={(e) => setEditingService({ ...editingService, level: e.target.value })}
                                >
                                    <MenuItem value="main">Main Service</MenuItem>
                                    <MenuItem value="sub">Sub Service</MenuItem>
                                    <MenuItem value="range">Range (Level 3)</MenuItem>
                                    <MenuItem value="lineitem">Line Item (Level 3)</MenuItem>
                                </Select>
                            </FormControl>
                            <TextField
                                label="Sort Order"
                                type="number"
                                fullWidth
                                value={editingService?.sortOrder || 0}
                                onChange={(e) => setEditingService({ ...editingService, sortOrder: parseInt(e.target.value) })}
                            />
                        </Stack>
                    </DialogContent>
                    <DialogActions sx={{ p: 3 }}>
                        <Button onClick={() => setEditorOpen(false)}>Cancel</Button>
                        <Button type="submit" variant="contained">Save Changes</Button>
                    </DialogActions>
                </form>
            </Dialog>

            {/* Pricing Drawer */}
            <Drawer anchor="right" open={pricingOpen} onClose={() => setPricingOpen(false)}>
                <Box sx={{ width: 400, p: 4, height: '100%', display: 'flex', flexDirection: 'column' }}>
                    <Typography variant="h5" fontWeight="bold">Update Pricing</Typography>
                    <Typography color="text.secondary" sx={{ mb: 4 }}>
                        {pricingService?.name} in {regions.find(r => r.id === selectedRegionId)?.name}
                    </Typography>

                    <Stack spacing={4} sx={{ flexGrow: 1 }}>
                        <TextField
                            label={`Local Price (${regions.find(r => r.id === selectedRegionId)?.localCurrency})`}
                            fullWidth
                            type="number"
                            value={pricingForm.local}
                            onChange={(e) => setPricingForm({ ...pricingForm, local: parseFloat(e.target.value) || 0 })}
                        />
                        <TextField
                            label="USD Reference Price"
                            fullWidth
                            type="number"
                            value={pricingForm.usdRef}
                            onChange={(e) => setPricingForm({ ...pricingForm, usdRef: parseFloat(e.target.value) || 0 })}
                        />
                        <Alert severity="info">
                            Price updates are effective immediately for all new proposals and re-generations.
                        </Alert>
                    </Stack>

                    <Button variant="contained" fullWidth size="large" onClick={handleSavePrice}>
                        Update Price Snapshot
                    </Button>
                </Box>
            </Drawer>

            {/* Deletion Confirmation */}
            <Dialog open={!!deleteConfirm} onClose={() => setDeleteConfirm(null)}>
                <DialogTitle>Delete Service</DialogTitle>
                <DialogContent>
                    <Typography>Are you sure you want to delete this service? All its children and associated prices will also be permanently removed.</Typography>
                </DialogContent>
                <DialogActions sx={{ p: 3 }}>
                    <Button onClick={() => setDeleteConfirm(null)}>Cancel</Button>
                    <Button onClick={handleDelete} variant="contained" color="error">Delete</Button>
                </DialogActions>
            </Dialog>

            <Snackbar open={toast.open} autoHideDuration={3000} onClose={() => setToast({ ...toast, open: false })}>
                <Alert severity={toast.severity}>{toast.message}</Alert>
            </Snackbar>
        </Box>
    );
}
