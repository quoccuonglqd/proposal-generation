"use client";

import React, { useEffect, useState } from 'react';
import {
    Typography, Box, Paper, Table, TableBody, TableCell,
    TableContainer, TableHead, TableRow, Select, MenuItem,
    Chip, CircularProgress, Alert, Snackbar
} from '@mui/material';
import { adminApi } from '../../services/api';

export default function UserManagement() {
    const [users, setUsers] = useState<any[]>([]);
    const [loading, setLoading] = useState(true);
    const [toast, setToast] = useState({ open: false, message: '', severity: 'success' as any });

    const fetchUsers = async () => {
        try {
            const res = await adminApi.getUsers();
            setUsers(res.data);
        } catch (err) {
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchUsers();
    }, []);

    const handleRoleChange = async (userId: string, roles: string[]) => {
        try {
            await adminApi.updateUserRoles(userId, roles);
            setToast({ open: true, message: 'User roles updated successfully', severity: 'success' });
            fetchUsers();
        } catch (err) {
            console.error(err);
            setToast({ open: true, message: 'Failed to update roles', severity: 'error' });
        }
    };

    if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>;

    return (
        <Box>
            <Typography variant="h4" fontWeight="bold" sx={{ mb: 4 }}>User Management</Typography>

            <TableContainer component={Paper}>
                <Table>
                    <TableHead>
                        <TableRow>
                            <TableCell>Display Name</TableCell>
                            <TableCell>Email</TableCell>
                            <TableCell>Roles</TableCell>
                            <TableCell align="right">Actions</TableCell>
                        </TableRow>
                    </TableHead>
                    <TableBody>
                        {users.map((user) => (
                            <TableRow key={user.id}>
                                <TableCell sx={{ fontWeight: 'medium' }}>{user.displayName}</TableCell>
                                <TableCell>{user.email}</TableCell>
                                <TableCell>
                                    <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                                        {user.roles.map((role: string) => (
                                            <Chip key={role} label={role} size="small" variant="outlined" color="primary" />
                                        ))}
                                    </Box>
                                </TableCell>
                                <TableCell align="right">
                                    <Select
                                        size="small"
                                        value={user.roles[0] || ''}
                                        onChange={(e) => handleRoleChange(user.id, [e.target.value])}
                                        sx={{ minWidth: 150 }}
                                    >
                                        <MenuItem value="Admin">Admin</MenuItem>
                                        <MenuItem value="ProposalCreator">Proposal Creator</MenuItem>
                                        <MenuItem value="Viewer">Viewer</MenuItem>
                                    </Select>
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </TableContainer>

            <Snackbar
                open={toast.open}
                autoHideDuration={4000}
                onClose={() => setToast({ ...toast, open: false })}
            >
                <Alert severity={toast.severity} sx={{ width: '100%' }}>
                    {toast.message}
                </Alert>
            </Snackbar>
        </Box>
    );
}
