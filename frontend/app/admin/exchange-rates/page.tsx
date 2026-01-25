"use client";

import React, { useEffect, useState } from 'react';
import {
    Typography, Box, Paper, Button, Stack, TextField,
    Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
    Dialog, DialogTitle, DialogContent, DialogActions,
    MenuItem, Select, FormControl, InputLabel,
    CircularProgress, Alert, Snackbar
} from '@mui/material';
import {
    Add as AddIcon,
    TrendingUp as RateIcon,
    Label as LabelIcon
} from '@mui/icons-material';
import { adminApi, catalogApi } from '../../services/api';

export default function ExchangeRateManagement() {
    const [rates, setRates] = useState<any[]>([]);
    const [regions, setRegions] = useState<any[]>([]);
    const [loading, setLoading] = useState(true);

    const [open, setOpen] = useState(false);
    const [form, setForm] = useState({ baseCurrency: 'USD', quoteCurrency: 'VND', rate: 25000, source: 'Manual', validFrom: new Date().toISOString() });

    const [toast, setToast] = useState({ open: false, message: '', severity: 'success' as any });

    const fetchData = async () => {
        setLoading(true);
        try {
            const [rRes, regRes] = await Promise.all([
                adminApi.getExchangeRates(),
                catalogApi.getRegions()
            ]);
            setRates(rRes.data);
            setRegions(regRes.data);
        } catch (err) {
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
    }, []);

    const handleSave = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await adminApi.updateDefaultRate(form);
            setOpen(false);
            fetchData();
            setToast({ open: true, message: 'Exchange rate updated', severity: 'success' });
        } catch (err) {
            setToast({ open: true, message: 'Failed to update rate', severity: 'error' });
        }
    };

    if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>;

    return (
        <Box>
            <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 4 }}>
                <Typography variant="h4" fontWeight="bold">Exchange Rate Rules</Typography>
                <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>
                    New Rule
                </Button>
            </Stack>

            <Alert severity="info" sx={{ mb: 3 }}>
                These rates are used during proposal generation to convert local prices to USD reference prices.
                Only one "Default" rule per currency pair is active at a time.
            </Alert>

            <TableContainer component={Paper}>
                <Table>
                    <TableHead>
                        <TableRow>
                            <TableCell>Pair</TableCell>
                            <TableCell>Rate</TableCell>
                            <TableCell>Source</TableCell>
                            <TableCell>Valid From</TableCell>
                            <TableCell>Status</TableCell>
                        </TableRow>
                    </TableHead>
                    <TableBody>
                        {rates.map((r) => (
                            <TableRow key={r.id}>
                                <TableCell sx={{ fontWeight: 'bold' }}>{r.baseCurrency} / {r.quoteCurrency}</TableCell>
                                <TableCell>{r.rate.toLocaleString()}</TableCell>
                                <TableCell>{r.source}</TableCell>
                                <TableCell>{new Date(r.validFrom).toLocaleDateString()}</TableCell>
                                <TableCell>
                                    <Typography variant="caption" color={r.isDefault ? 'success.main' : 'text.disabled'} sx={{ fontWeight: r.isDefault ? 'bold' : 'normal' }}>
                                        {r.isDefault ? 'PRIMARY DEFAULT' : 'HISTORICAL'}
                                    </Typography>
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </TableContainer>

            <Dialog open={open} onClose={() => setOpen(false)}>
                <form onSubmit={handleSave}>
                    <DialogTitle>Add New Default Rate</DialogTitle>
                    <DialogContent sx={{ pt: 2 }}>
                        <Stack spacing={3} sx={{ minWidth: 400, mt: 1 }}>
                            <Stack direction="row" spacing={2}>
                                <TextField label="Base" value="USD" disabled fullWidth />
                                <FormControl fullWidth>
                                    <InputLabel>Quote Currency</InputLabel>
                                    <Select
                                        label="Quote Currency"
                                        value={form.quoteCurrency}
                                        onChange={(e) => setForm({ ...form, quoteCurrency: e.target.value })}
                                    >
                                        <MenuItem value="VND">VND (Vietnam)</MenuItem>
                                        <MenuItem value="THB">THB (Thailand)</MenuItem>
                                        <MenuItem value="SGD">SGD (Singapore)</MenuItem>
                                    </Select>
                                </FormControl>
                            </Stack>
                            <TextField
                                label="Exchange Rate"
                                type="number"
                                fullWidth
                                value={form.rate}
                                onChange={(e) => setForm({ ...form, rate: parseFloat(e.target.value) || 0 })}
                                required
                            />
                            <TextField
                                label="Source Name"
                                fullWidth
                                placeholder="e.g. Vietcombank, XE.com"
                                value={form.source}
                                onChange={(e) => setForm({ ...form, source: e.target.value })}
                            />
                        </Stack>
                    </DialogContent>
                    <DialogActions sx={{ p: 3 }}>
                        <Button onClick={() => setOpen(false)}>Cancel</Button>
                        <Button type="submit" variant="contained">Set as New Default</Button>
                    </DialogActions>
                </form>
            </Dialog>

            <Snackbar open={toast.open} autoHideDuration={3000} onClose={() => setToast({ ...toast, open: false })}>
                <Alert severity={toast.severity}>{toast.message}</Alert>
            </Snackbar>
        </Box>
    );
}
