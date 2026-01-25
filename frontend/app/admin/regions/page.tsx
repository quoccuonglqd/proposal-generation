"use client";

import React, { useEffect, useState } from 'react';
import {
    Box,
    Typography,
    Paper,
    Table,
    TableBody,
    TableCell,
    TableContainer,
    TableHead,
    TableRow,
    Button,
    Stack,
    Chip,
    IconButton,
    CircularProgress,
    Dialog,
    DialogTitle,
    DialogContent,
    DialogActions,
    TextField,
    Snackbar,
    Alert
} from '@mui/material';
import { catalogApi } from '../../services/api';
import {
    Add as AddIcon,
    Edit as EditIcon,
    Delete as DeleteIcon
} from '@mui/icons-material';

export default function RegionsAdmin() {
    const [regions, setRegions] = useState<any[]>([]);
    const [loading, setLoading] = useState(true);
    const [open, setOpen] = useState(false);
    const [editingRegion, setEditingRegion] = useState<any>(null);
    const [toast, setToast] = useState({ open: false, message: '', severity: 'success' as any });

    const fetchRegions = async () => {
        setLoading(true);
        try {
            const res = await catalogApi.getRegions();
            setRegions(res.data);
        } catch (err) {
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchRegions();
    }, []);

    const handleSave = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            if (editingRegion.id) {
                await catalogApi.updateRegion(editingRegion.id, editingRegion);
            } else {
                await catalogApi.createRegion(editingRegion);
            }
            setOpen(false);
            fetchRegions();
            setToast({ open: true, message: 'Region saved successfully', severity: 'success' });
        } catch (err) {
            setToast({ open: true, message: 'Failed to save region', severity: 'error' });
        }
    };

    const handleDelete = async (id: string) => {
        if (window.confirm('Deactivate this region? it will no longer be available for new proposals.')) {
            try {
                await catalogApi.deleteRegion(id);
                fetchRegions();
                setToast({ open: true, message: 'Region deactivated', severity: 'success' });
            } catch (err) {
                setToast({ open: true, message: 'Failed to deactivate region', severity: 'error' });
            }
        }
    };

    if (loading && regions.length === 0) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>;

    return (
        <Box>
            <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 4 }}>
                <Typography variant="h4" fontWeight="bold">Regions Management</Typography>
                <Button variant="contained" startIcon={<AddIcon />} onClick={() => {
                    setEditingRegion({ code: '', name: '', localCurrency: 'VND', isActive: true });
                    setOpen(true);
                }}>
                    Add Region
                </Button>
            </Stack>

            <TableContainer component={Paper}>
                <Table>
                    <TableHead>
                        <TableRow>
                            <TableCell>Code</TableCell>
                            <TableCell>Name</TableCell>
                            <TableCell>Local Currency</TableCell>
                            <TableCell>Status</TableCell>
                            <TableCell align="right">Actions</TableCell>
                        </TableRow>
                    </TableHead>
                    <TableBody>
                        {regions.map((region) => (
                            <TableRow key={region.id}>
                                <TableCell sx={{ fontWeight: 'bold' }}>{region.code}</TableCell>
                                <TableCell>{region.name}</TableCell>
                                <TableCell>{region.localCurrency}</TableCell>
                                <TableCell>
                                    <Chip
                                        label={region.isActive ? 'Active' : 'Inactive'}
                                        color={region.isActive ? 'success' : 'default'}
                                        size="small"
                                    />
                                </TableCell>
                                <TableCell align="right">
                                    <Stack direction="row" spacing={1} justifyContent="flex-end">
                                        <IconButton size="small" onClick={() => {
                                            setEditingRegion(region);
                                            setOpen(true);
                                        }}>
                                            <EditIcon fontSize="small" />
                                        </IconButton>
                                        {region.isActive && (
                                            <IconButton size="small" color="error" onClick={() => handleDelete(region.id)}>
                                                <DeleteIcon fontSize="small" />
                                            </IconButton>
                                        )}
                                    </Stack>
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </TableContainer>

            <Dialog open={open} onClose={() => setOpen(false)}>
                <form onSubmit={handleSave}>
                    <DialogTitle>{editingRegion?.id ? 'Edit Region' : 'Add New Region'}</DialogTitle>
                    <DialogContent sx={{ pt: 2 }}>
                        <Stack spacing={3} sx={{ minWidth: 400, mt: 1 }}>
                            <TextField
                                label="Region Name"
                                fullWidth
                                value={editingRegion?.name || ''}
                                onChange={(e) => setEditingRegion({ ...editingRegion, name: e.target.value })}
                                required
                            />
                            <TextField
                                label="ISO Code"
                                placeholder="e.g. VN, TH, SG"
                                fullWidth
                                value={editingRegion?.code || ''}
                                onChange={(e) => setEditingRegion({ ...editingRegion, code: e.target.value.toUpperCase() })}
                                required
                            />
                            <TextField
                                label="Local Currency"
                                placeholder="e.g. VND, THB, SGD"
                                fullWidth
                                value={editingRegion?.localCurrency || ''}
                                onChange={(e) => setEditingRegion({ ...editingRegion, localCurrency: e.target.value.toUpperCase() })}
                                required
                            />
                        </Stack>
                    </DialogContent>
                    <DialogActions sx={{ p: 3 }}>
                        <Button onClick={() => setOpen(false)}>Cancel</Button>
                        <Button type="submit" variant="contained">Save Region</Button>
                    </DialogActions>
                </form>
            </Dialog>

            <Snackbar open={toast.open} autoHideDuration={3000} onClose={() => setToast({ ...toast, open: false })}>
                <Alert severity={toast.severity}>{toast.message}</Alert>
            </Snackbar>
        </Box>
    );
}

